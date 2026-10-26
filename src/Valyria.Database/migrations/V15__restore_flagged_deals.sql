-- =============================================================================
-- V15__restore_flagged_deals.sql
-- VEP 3.0: Let admins restore a deal that a flag cancelled
--
-- Admins can now put a deal cancelled by a 5_day_replacement or aid_failed
-- flag back into effect (DealsRepository.RestoreFlaggedDealAsync). Restoring
-- sets the deal back to active, clears its deal_state so the 5-day clock
-- starts fresh, and resolves the deal's cancellation flags.
--
-- A deal cancelled over a failed leg would be cancelled again at the next
-- daily check, since check_deal_inactivity (V10) cancels any active deal with
-- a failed leg. Restoring marks those failures as excused, and the check now
-- skips excused failures. The legs still count as failed everywhere else, so
-- the turn doesn't move on and the deal's history still shows what happened.
--
-- Changes:
-- 1. deal_activity gains failure_excused
-- 2. Rewritten procedure: check_deal_inactivity (only step 2's filter changed)
-- =============================================================================

USE `vep_db`;

ALTER TABLE `deal_activity`
    ADD COLUMN `failure_excused` BOOLEAN NOT NULL DEFAULT FALSE
        COMMENT 'An admin restored the deal this failed leg cancelled, so it no longer cancels the deal'
        AFTER `failure_reason`;

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
-- 3. Cancel deals held up 5+ days; flag the responsible account
-- 4. Warn accounts holding up a deal 1+ days
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

    -- Step 3: Held up 5+ days. Cancel the deal and flag the responsible account
    DROP TEMPORARY TABLE IF EXISTS tmp_replace_deals;
    CREATE TEMPORARY TABLE tmp_replace_deals AS
    SELECT
        ds.deal_id,
        ds.responsible_account_id AS flagged_account_id,
        IF(dp.account_1_id = ds.responsible_account_id, dp.account_2_id, dp.account_1_id) AS innocent_account_id,
        IF(ds.state = 'awaiting_send', 'awaiting_send', ds.blocked_reason) AS flag_reason
    FROM deal_state ds
    JOIN deal_participants dp ON dp.deal_id = ds.deal_id
    WHERE dp.status = 'active'
        AND ds.responsible_account_id IS NOT NULL
        AND DATEDIFF(v_today, ds.responsible_since) >= 5;

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

    -- Step 4: Held up 1+ days. Warn the responsible account, once per stretch
    INSERT INTO deal_flag (deal_id, flag_type, flag_reason, flagged_account_id, innocent_account_id)
    SELECT
        ds.deal_id,
        '1_day_inactive',
        IF(ds.state = 'awaiting_send', 'awaiting_send', ds.blocked_reason),
        ds.responsible_account_id,
        IF(dp.account_1_id = ds.responsible_account_id, dp.account_2_id, dp.account_1_id)
    FROM deal_state ds
    JOIN deal_participants dp ON dp.deal_id = ds.deal_id
    WHERE dp.status = 'active'
        AND ds.responsible_account_id IS NOT NULL
        AND DATEDIFF(v_today, ds.responsible_since) >= 1
        AND NOT EXISTS (
            SELECT 1 FROM deal_flag df
            WHERE df.deal_id = ds.deal_id
                AND df.flag_type = '1_day_inactive'
                AND df.flagged_account_id = ds.responsible_account_id
                AND df.resolved_on IS NULL
        );
END$$

DELIMITER ;

-- =============================================================================
-- End of V15 Migration
-- =============================================================================
