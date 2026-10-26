-- =============================================================================
-- V16__deal_extensions.sql
-- VEP 3.0: Let admins give an account more time before a deal is cancelled
--
-- Resolving a 1-day warning never moved the 5-day cancellation: the clock is
-- deal_state.responsible_since, which only resets when someone else becomes
-- responsible. Admins can now grant an extension to the account holding up
-- a deal (DealsRepository.ExtendFlaggedDealAsync):
--   five_days        the cancellation moves 5 days past its current date.
--                    The clock then runs as usual against the new date:
--                    warned again a day past the old date, cancelled on the
--                    new one
--   until_next_leg   no warnings and no cancellation at all
--
-- Either kind applies only while the same account is holding up the deal and
-- no new leg has been recorded. Once a leg is recorded or someone else
-- becomes responsible, the deal goes back to the normal rules, so a partner
-- who becomes responsible gets a normal 5-day clock.
--
-- deal_deadline works out each deal's clock with any extension applied. The
-- daily check, the mass messages and the deal pages all read it, so they
-- agree on when a deal will be cancelled.
--
-- Changes:
-- 1. New table: deal_extension
-- 2. New view: deal_deadline
-- 3. Rewritten procedure: check_deal_inactivity (steps 3 and 4 use
--    deal_deadline; new step 5 ends extensions that no longer apply)
-- =============================================================================

USE `vep_db`;

CREATE TABLE IF NOT EXISTS `deal_extension` (
    `id` INT AUTO_INCREMENT PRIMARY KEY,
    `deal_id` INT NOT NULL,
    `flag_id` INT NULL COMMENT 'The warning the extension was granted from',
    `account_id` INT NOT NULL COMMENT 'Account holding up the deal when the extension was granted',
    `kind` ENUM('five_days', 'until_next_leg') NOT NULL,
    `extended_until` DATE NULL COMMENT 'CN date the deal is now cancelled on (five_days only)',
    `legs_counted_at_grant` INT NOT NULL COMMENT 'deal_state.legs_counted when granted; a new leg ends the extension',
    `granted_on` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `granted_by` VARCHAR(50) NOT NULL,
    `admin_notes` TEXT NULL,
    `ended_on` DATETIME NULL,
    `end_reason` ENUM('leg_recorded', 'responsibility_moved', 'deal_ended', 'superseded') NULL,

    FOREIGN KEY (`deal_id`) REFERENCES `deal`(`id`) ON DELETE CASCADE,
    FOREIGN KEY (`flag_id`) REFERENCES `deal_flag`(`id`) ON DELETE SET NULL,
    FOREIGN KEY (`account_id`) REFERENCES `account`(`id`) ON DELETE CASCADE,

    INDEX `idx_open` (`deal_id`, `ended_on`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Extra time admins have given an account to stop holding up a deal';

-- =============================================================================
-- VIEW: deal_deadline
-- Purpose: Each deal's cancellation clock, with any extension that still
-- applies
--
-- clock_since is the day the 5-day clock started: responsible_since
-- normally, 5 days before extended_until for five_days, and NULL (no clock)
-- for until_next_leg or when nobody is responsible. The deal is cancelled at
-- the daily check on cancels_on, and the account is warned from
-- clock_since + 1 day. Granting supersedes any open extension, so a deal has
-- at most one open extension.
-- =============================================================================
CREATE OR REPLACE VIEW `deal_deadline` AS
SELECT
    x.deal_id,
    x.extension_id,
    x.extension_kind,
    x.extended_until,
    x.extended_by,
    x.clock_since,
    x.clock_since + INTERVAL 5 DAY AS cancels_on
FROM (
    SELECT
        ds.deal_id,
        e.id AS extension_id,
        e.kind AS extension_kind,
        e.extended_until,
        e.granted_by AS extended_by,
        CASE
            WHEN ds.responsible_account_id IS NULL THEN NULL
            WHEN e.kind = 'until_next_leg' THEN NULL
            WHEN e.kind = 'five_days' THEN e.extended_until - INTERVAL 5 DAY
            ELSE ds.responsible_since
        END AS clock_since
    FROM deal_state ds
    LEFT JOIN deal_extension e ON e.deal_id = ds.deal_id
        AND e.ended_on IS NULL
        AND e.account_id = ds.responsible_account_id
        AND ds.legs_counted <= e.legs_counted_at_grant
) x;

DELIMITER $$

-- =============================================================================
-- PROCEDURE: check_deal_inactivity
-- Purpose: Flag and cancel deals from deal_state and failed legs
--
-- Run after track_deal_activity and update_deal_state, once per day when the
-- day's aid file is loaded. Run update_deal_state again afterwards so
-- cancelled deals drop out of deal_state.
--
-- Flow:
-- 1. Resolve 1-day warnings whose account is no longer responsible
-- 2. Cancel deals with a failed leg that hasn't been excused; flag both
--    accounts
-- 3. Cancel deals whose deadline (deal_deadline.cancels_on) has come; flag
--    the responsible account
-- 4. Warn accounts a day or more into their clock
-- 5. End extensions that no longer apply
-- =============================================================================
DROP PROCEDURE IF EXISTS `check_deal_inactivity`$$

CREATE PROCEDURE `check_deal_inactivity`()
MODIFIES SQL DATA
BEGIN
    DECLARE v_today DATE DEFAULT DATE(CONVERT_TZ(UTC_TIMESTAMP(), '+00:00', '-05:00'));

    -- Step 1: Resolve warnings that no longer apply (aid sent, slots freed,
    -- or the deal ended)
    UPDATE deal_flag df
    LEFT JOIN deal_state ds ON ds.deal_id = df.deal_id
    LEFT JOIN deal d ON d.id = df.deal_id
    SET
        df.resolved_on = CURRENT_TIMESTAMP,
        df.resolved_by = 'system',
        df.admin_notes = 'Resolved automatically: account no longer holding up the deal'
    WHERE df.flag_type = '1_day_inactive'
        AND df.resolved_on IS NULL
        AND (
            d.status <> 'active'
            OR ds.responsible_account_id IS NULL
            OR ds.responsible_account_id <> df.flagged_account_id
        );

    -- Step 2: Failed legs. One flag row per account per deal, using the
    -- deal's most recent failure. Failures excused by restoring the deal
    -- are skipped
    DROP TEMPORARY TABLE IF EXISTS tmp_failed_flags;
    CREATE TEMPORARY TABLE tmp_failed_flags AS
    WITH failed AS (
        SELECT
            dp.deal_id,
            dp.account_1_id,
            dp.account_2_id,
            CONCAT('aid_', da.failure_reason) AS flag_reason,
            ROW_NUMBER() OVER (PARTITION BY dp.deal_id ORDER BY da.failed_on DESC, da.leg_number DESC) AS rn
        FROM deal_activity da
        JOIN deal_participants dp ON dp.deal_id = da.deal_id
        WHERE dp.status = 'active'
            AND da.failed_on IS NOT NULL
            AND NOT da.failure_excused
    )
    SELECT deal_id, flag_reason, account_1_id AS flagged_account_id, account_2_id AS innocent_account_id
    FROM failed WHERE rn = 1
    UNION ALL
    SELECT deal_id, flag_reason, account_2_id, account_1_id
    FROM failed WHERE rn = 1;

    INSERT INTO deal_flag (deal_id, flag_type, flag_reason, flagged_account_id, innocent_account_id)
    SELECT deal_id, 'aid_failed', flag_reason, flagged_account_id, innocent_account_id
    FROM tmp_failed_flags;

    UPDATE deal d
    JOIN (SELECT DISTINCT deal_id, flag_reason FROM tmp_failed_flags) f ON f.deal_id = d.id
    SET
        d.status = 'cancelled',
        d.cancelled_on = CURRENT_TIMESTAMP,
        d.cancelled_by = 'system',
        d.cancellation_reason = IF(f.flag_reason = 'aid_cancelled',
            'Aid offer was cancelled - both accounts flagged for admin review',
            'Aid offer expired without being accepted - both accounts flagged for admin review');

    DROP TEMPORARY TABLE IF EXISTS tmp_failed_flags;

    -- Step 3: Deadline reached (5 days held up, plus any extension). Cancel
    -- the deal and flag the responsible account
    DROP TEMPORARY TABLE IF EXISTS tmp_replace_deals;
    CREATE TEMPORARY TABLE tmp_replace_deals AS
    SELECT
        ds.deal_id,
        ds.responsible_account_id AS flagged_account_id,
        IF(dp.account_1_id = ds.responsible_account_id, dp.account_2_id, dp.account_1_id) AS innocent_account_id,
        IF(ds.state = 'awaiting_send', 'awaiting_send', ds.blocked_reason) AS flag_reason
    FROM deal_state ds
    JOIN deal_participants dp ON dp.deal_id = ds.deal_id
    JOIN deal_deadline dd ON dd.deal_id = ds.deal_id
    WHERE dp.status = 'active'
        AND ds.responsible_account_id IS NOT NULL
        AND dd.cancels_on <= v_today;

    INSERT INTO deal_flag (deal_id, flag_type, flag_reason, flagged_account_id, innocent_account_id)
    SELECT deal_id, '5_day_replacement', flag_reason, flagged_account_id, innocent_account_id
    FROM tmp_replace_deals;

    UPDATE deal d
    JOIN tmp_replace_deals r ON r.deal_id = d.id
    SET
        d.status = 'cancelled',
        d.cancelled_on = CURRENT_TIMESTAMP,
        d.cancelled_by = 'system',
        d.cancellation_reason = CASE r.flag_reason
            WHEN 'awaiting_send' THEN 'Expected aid not sent for 5+ days - automatically cancelled'
            WHEN 'out_of_sequence_aid' THEN 'Out-of-sequence aid blocked the deal for 5+ days - automatically cancelled'
            ELSE 'Aid slots full of non-deal aid for 5+ days - automatically cancelled'
        END;

    DROP TEMPORARY TABLE IF EXISTS tmp_replace_deals;

    -- Step 4: A day or more into the clock. Warn the responsible account,
    -- once per stretch. An extension pushes the clock back, or stops it
    INSERT INTO deal_flag (deal_id, flag_type, flag_reason, flagged_account_id, innocent_account_id)
    SELECT
        ds.deal_id,
        '1_day_inactive',
        IF(ds.state = 'awaiting_send', 'awaiting_send', ds.blocked_reason),
        ds.responsible_account_id,
        IF(dp.account_1_id = ds.responsible_account_id, dp.account_2_id, dp.account_1_id)
    FROM deal_state ds
    JOIN deal_participants dp ON dp.deal_id = ds.deal_id
    JOIN deal_deadline dd ON dd.deal_id = ds.deal_id
    WHERE dp.status = 'active'
        AND ds.responsible_account_id IS NOT NULL
        AND DATEDIFF(v_today, dd.clock_since) >= 1
        AND NOT EXISTS (
            SELECT 1 FROM deal_flag df
            WHERE df.deal_id = ds.deal_id
                AND df.flag_type = '1_day_inactive'
                AND df.flagged_account_id = ds.responsible_account_id
                AND df.resolved_on IS NULL
        );

    -- Step 5: End extensions that no longer apply, so they can't apply
    -- again to a later stretch
    UPDATE deal_extension e
    JOIN deal d ON d.id = e.deal_id
    LEFT JOIN deal_state ds ON ds.deal_id = e.deal_id
    SET
        e.ended_on = CURRENT_TIMESTAMP,
        e.end_reason = CASE
            WHEN d.status <> 'active' THEN 'deal_ended'
            WHEN ds.legs_counted > e.legs_counted_at_grant THEN 'leg_recorded'
            ELSE 'responsibility_moved'
        END
    WHERE e.ended_on IS NULL
        AND (
            d.status <> 'active'
            OR ds.deal_id IS NULL
            OR ds.legs_counted > e.legs_counted_at_grant
            OR ds.responsible_account_id IS NULL
            OR ds.responsible_account_id <> e.account_id
        );
END$$

DELIMITER ;

-- =============================================================================
-- End of V16 Migration
-- =============================================================================
