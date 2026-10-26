-- =============================================================================
-- V17__team_event_bonus_slots.sql
-- VEP 3.0: Extra aid slot while a team's +1 aid slot event is in effect
--
-- CN teams (colors) can pass a senate proposal that gives every nation on the
-- team an extra aid slot for 30 days. Admins record the event once a majority
-- of the team's senators approve it, and VEP gives each account on the team a
-- bonus slot (slot 7) for as long as the event lasts:
--   - The event is in effect from the CN date it was approved through 30 days
--     later (a proposal passed on 10/6 lasts through 11/5)
--   - Nations move between teams freely, so the bonus follows the nation's
--     current team on every loader run. Accounts on hold get none
--   - The slot counts towards slots_total everywhere 4 + fm + dra did
--   - Each leg needs the previous one to clear (10 days), so a bonus slot is
--     only matched while the deal's legs can all be sent before the event
--     ends: through day 30 for one-way slots, day 20 for 1:1 slots and day 10
--     for 1:2 slots (10 days earlier per leg after the first)
--   - When the event ends, the nation leaves the team, or an admin deletes the
--     event, the deal using the bonus slot is cancelled. The slot row is kept
--     if it has deal history (deleting it would cascade to its deals), and is
--     reused the next time the account gets a bonus
--
-- Changes:
-- 1. New table: team_event
-- 2. slot: is_team_bonus, team_event_id, slot_number up to 7
-- 3. New views: active_team_event, account_team_event, account_slot_total,
--    team_bonus_slot
-- 4. Rewritten views: account_deal_summary, deal_sequence (slot totals from
--    account_slot_total)
-- 5. Rewritten procedures: regenerate_slots_for_account (leaves the bonus
--    slot alone), match_deals_run (skips bonus slots that aren't in effect or
--    are past their matching cutoff)
-- 6. New procedure: sync_team_event_slots
-- =============================================================================

USE `vep_db`;

-- =============================================================================
-- TABLE: team_event
-- Purpose: Team senate events that change aid slots
--
-- Only plus_one_aid_slot is supported for now. Events are deleted softly so
-- the reason a bonus deal was cancelled can still be traced. The repository
-- rejects teams outside 1-12 (13 is None, which has no senate), approval
-- dates in the future, and events that overlap another for the same team
-- =============================================================================
CREATE TABLE IF NOT EXISTS `team_event` (
    `id` INT AUTO_INCREMENT PRIMARY KEY,
    `team` TINYINT NOT NULL COMMENT 'cybernations_db.nation.team: 1 Aqua through 12 Pink',
    `event_type` ENUM('plus_one_aid_slot') NOT NULL DEFAULT 'plus_one_aid_slot',
    `approved_on` DATE NOT NULL COMMENT 'CN date a majority of the team''s senators passed the proposal',
    `ends_on` DATE GENERATED ALWAYS AS (`approved_on` + INTERVAL 30 DAY) STORED
        COMMENT 'Last CN date the event is in effect',
    `created_on` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `created_by` VARCHAR(50) NOT NULL,
    `deleted_on` DATETIME NULL,
    `deleted_by` VARCHAR(50) NULL,

    CONSTRAINT `chk_team_event_team` CHECK (`team` BETWEEN 1 AND 12),

    INDEX `idx_team_dates` (`team`, `approved_on`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Team senate events that change aid slots';

-- =============================================================================
-- TABLE: slot
-- Slot 7 is the team bonus slot. team_event_id is the event it was last in
-- effect for, which sync_team_event_slots uses to explain cancellations
-- =============================================================================
ALTER TABLE `slot`
    DROP CHECK `chk_slot_number`;

ALTER TABLE `slot`
    ADD COLUMN `is_team_bonus` BOOLEAN NOT NULL DEFAULT FALSE
        COMMENT 'Extra slot while a team event is in effect' AFTER `slot_number`,
    ADD COLUMN `team_event_id` INT NULL
        COMMENT 'Team event the bonus slot was last in effect for' AFTER `is_team_bonus`,
    ADD CONSTRAINT `fk_slot_team_event` FOREIGN KEY (`team_event_id`) REFERENCES `team_event`(`id`),
    ADD CONSTRAINT `chk_slot_number` CHECK (`slot_number` BETWEEN 1 AND 7),
    ADD CONSTRAINT `chk_team_bonus_slot` CHECK (`is_team_bonus` = (`slot_number` = 7));

-- =============================================================================
-- VIEW: active_team_event
-- Purpose: The +1 aid slot event in effect today (CN date) for each team.
-- Overlaps are rejected on entry; MAX keeps one per team regardless
-- =============================================================================
CREATE OR REPLACE VIEW `active_team_event` AS
SELECT te.*
FROM team_event te
JOIN (
    SELECT team, MAX(id) AS id
    FROM team_event
    WHERE deleted_on IS NULL
        AND event_type = 'plus_one_aid_slot'
        AND DATE(CONVERT_TZ(UTC_TIMESTAMP(), '+00:00', '-05:00')) BETWEEN approved_on AND ends_on
    GROUP BY team
) latest ON latest.id = te.id;

-- =============================================================================
-- VIEW: account_team_event
-- Purpose: Accounts whose nation is on a team with an event in effect.
-- Accounts on hold, or whose nation was deleted, get no bonus
-- =============================================================================
CREATE OR REPLACE VIEW `account_team_event` AS
SELECT
    a.id AS account_id,
    ate.id AS team_event_id,
    ate.team,
    ate.approved_on,
    ate.ends_on
FROM account a
JOIN cybernations_db.nation n ON n.id = a.nation_id
JOIN active_team_event ate ON ate.team = n.team
WHERE a.va <> 'H';

-- =============================================================================
-- VIEW: account_slot_total
-- Purpose: Aid slots per account: 4, +1 for fm, +1 for dra, +1 while a team
-- event is in effect. Everything that counted 4 + fm + dra uses this
-- =============================================================================
CREATE OR REPLACE VIEW `account_slot_total` AS
SELECT
    a.id AS account_id,
    4 + IF(a.fm = 1, 1, 0) + IF(a.dra = 1, 1, 0) AS base_slots,
    IF(ate.account_id IS NULL, 0, 1) AS team_bonus_slots,
    4 + IF(a.fm = 1, 1, 0) + IF(a.dra = 1, 1, 0) + IF(ate.account_id IS NULL, 0, 1) AS slots_total
FROM account a
LEFT JOIN account_team_event ate ON ate.account_id = a.id;

-- =============================================================================
-- VIEW: team_bonus_slot
-- Purpose: Bonus slots in effect today, with the last CN date a new deal can
-- be matched into each. A bonus slot not listed here is inactive
--
-- Legs: a slot that both sends and receives needs every cycle of a round
-- (1:1 is 2 legs, 1:2 is 3); a one-way slot needs 1. Each leg after the
-- first waits 10 days for the previous one to clear
-- =============================================================================
CREATE OR REPLACE VIEW `team_bonus_slot` AS
SELECT
    s.id AS slot_id,
    s.account_id,
    ate.team_event_id,
    ate.team,
    ate.approved_on,
    ate.ends_on,
    ate.ends_on - INTERVAL (10 * (
        IF(s.tech_sent_cycles + s.cash_sent_cycles > 0 AND s.tech_received_cycles + s.cash_received_cycles > 0,
            s.tech_sent_cycles + s.tech_received_cycles + s.cash_sent_cycles + s.cash_received_cycles,
            1) - 1)) DAY AS last_match_on
FROM slot s
JOIN account_team_event ate ON ate.account_id = s.account_id
WHERE s.is_team_bonus;

-- =============================================================================
-- VIEW: account_deal_summary
-- Same columns as V7, with slots_total from account_slot_total
-- =============================================================================
CREATE OR REPLACE VIEW `account_deal_summary` AS
SELECT
    a.id AS account_id,
    a.nation_id,
    a.va AS role,
    a.fm,
    a.dra,
    ast.slots_total,
    COALESCE(u.slots_used, 0) AS slots_used,
    GREATEST(ast.slots_total - COALESCE(u.slots_used, 0), 0) AS slots_available,
    COALESCE(u.slots_used, 0) >= ast.slots_total AS slots_full
FROM account a
JOIN account_slot_total ast ON ast.account_id = a.id
LEFT JOIN (
    SELECT s.account_id, COUNT(DISTINCT s.id) AS slots_used
    FROM slot s
    JOIN deal d ON (d.slot_1_id = s.id OR d.slot_2_id = s.id)
    WHERE d.status = 'active'
    GROUP BY s.account_id
) u ON u.account_id = a.id;

-- =============================================================================
-- VIEW: deal_sequence
-- Same as V9, with slots_total_1/2 from account_slot_total
-- =============================================================================
CREATE OR REPLACE VIEW `deal_sequence` AS
SELECT
    b.deal_id,
    b.created_on,
    b.account_1_id,
    b.account_2_id,
    b.nation_1_id,
    b.nation_2_id,
    b.slots_total_1,
    b.slots_total_2,
    b.legs_per_round_1,
    b.legs_per_round_2,
    b.account_1_first,
    b.legs_counted,
    b.legs_linked,
    b.last_leg_date,
    b.first_leg_date,
    b.legs_per_round_1 + b.legs_per_round_2 AS round_length,
    -- Position in the current round of the next leg, 0-based
    MOD(b.legs_counted, NULLIF(b.legs_per_round_1 + b.legs_per_round_2, 0)) AS round_position,
    CASE
        WHEN b.legs_per_round_1 + b.legs_per_round_2 = 0 THEN NULL
        WHEN MOD(b.legs_counted, b.legs_per_round_1 + b.legs_per_round_2)
             < IF(b.account_1_first, b.legs_per_round_1, b.legs_per_round_2)
            THEN b.account_1_first
        ELSE NOT b.account_1_first
    END AS next_sender_is_account_1
FROM (
    SELECT
        d.id AS deal_id,
        d.created_on,
        s1.account_id AS account_1_id,
        s2.account_id AS account_2_id,
        a1.nation_id AS nation_1_id,
        a2.nation_id AS nation_2_id,
        t1.slots_total AS slots_total_1,
        t2.slots_total AS slots_total_2,
        s1.tech_sent_cycles + s1.cash_sent_cycles AS legs_per_round_1,
        s2.tech_sent_cycles + s2.cash_sent_cycles AS legs_per_round_2,
        -- The cash sender goes first. If neither or both send cash, whichever
        -- side sends anything goes first, falling back to account 1
        CASE
            WHEN s1.cash_sent_cycles > 0 AND s2.cash_sent_cycles = 0 THEN TRUE
            WHEN s2.cash_sent_cycles > 0 AND s1.cash_sent_cycles = 0 THEN FALSE
            WHEN s1.tech_sent_cycles + s1.cash_sent_cycles = 0 THEN FALSE
            ELSE TRUE
        END AS account_1_first,
        COALESCE(l.legs_counted, 0) AS legs_counted,
        COALESCE(l.legs_linked, 0) AS legs_linked,
        l.last_leg_date,
        l.first_leg_date
    FROM deal d
    JOIN slot s1 ON d.slot_1_id = s1.id
    JOIN slot s2 ON d.slot_2_id = s2.id
    JOIN account a1 ON s1.account_id = a1.id
    JOIN account a2 ON s2.account_id = a2.id
    JOIN account_slot_total t1 ON t1.account_id = a1.id
    JOIN account_slot_total t2 ON t2.account_id = a2.id
    LEFT JOIN (
        SELECT
            deal_id,
            SUM(failed_on IS NULL) AS legs_counted,
            COUNT(*) AS legs_linked,
            MAX(activity_date) AS last_leg_date,
            MIN(activity_date) AS first_leg_date
        FROM deal_activity
        GROUP BY deal_id
    ) l ON l.deal_id = d.id
    WHERE d.status = 'active'
) b;

DELIMITER $$

-- =============================================================================
-- PROCEDURE: regenerate_slots_for_account
-- Same as V7, except fm/dra changes leave the team bonus slot alone. A role
-- change still resets it with the rest
-- =============================================================================
DROP PROCEDURE IF EXISTS `regenerate_slots_for_account`$$

CREATE PROCEDURE `regenerate_slots_for_account`(
    IN p_account_id INT,
    IN p_role_changed BOOLEAN
)
MODIFIES SQL DATA
BEGIN
    DECLARE v_slot_count INT;

    SELECT base_slots
    INTO v_slot_count
    FROM account_slot_total
    WHERE account_id = p_account_id;

    IF p_role_changed THEN
        UPDATE deal d
        JOIN slot s ON (d.slot_1_id = s.id OR d.slot_2_id = s.id)
        SET
            d.status = 'cancelled',
            d.cancelled_on = CURRENT_TIMESTAMP,
            d.cancelled_by = 'system',
            d.cancellation_reason = 'Account role changed'
        WHERE s.account_id = p_account_id
            AND d.status = 'active';

        UPDATE slot s
        JOIN account a ON s.account_id = a.id
        SET
            s.tech_sent_cycles = CASE WHEN a.va IN ('S', 'F') THEN 1 ELSE 0 END,
            s.tech_received_cycles = CASE WHEN a.va IN ('B', 'R') THEN 1 ELSE 0 END,
            s.cash_sent_cycles = CASE WHEN a.va IN ('B', 'D') THEN 1 ELSE 0 END,
            s.cash_received_cycles = CASE WHEN a.va IN ('S', 'C') THEN 1 ELSE 0 END,
            s.updated_by = NULL
        WHERE s.account_id = p_account_id;
    END IF;

    -- Remove extra slots (fm/dra turned off)
    UPDATE deal d
    JOIN slot s ON (d.slot_1_id = s.id OR d.slot_2_id = s.id)
    SET
        d.status = 'cancelled',
        d.cancelled_on = CURRENT_TIMESTAMP,
        d.cancelled_by = 'system',
        d.cancellation_reason = 'Account slot removed'
    WHERE s.account_id = p_account_id
        AND s.slot_number > v_slot_count
        AND NOT s.is_team_bonus
        AND d.status = 'active';

    DELETE FROM slot
    WHERE account_id = p_account_id
        AND slot_number > v_slot_count
        AND NOT is_team_bonus;

    -- Add missing slots (fm/dra turned on)
    CALL generate_slots_for_account(p_account_id);
END$$

-- =============================================================================
-- PROCEDURE: sync_team_event_slots
-- Purpose: Bring team bonus slots in line with the team events in effect today
--
-- Parameters:
--   OUT p_deals_cancelled - Deals cancelled because their bonus slot ended
--
-- Run by the data loader before matching, and by the admin API whenever an
-- event is added or deleted.
-- 1. Add a bonus slot (role defaults) for accounts that need one
-- 2. Record which event each bonus slot is in effect for
-- 3. Cancel deals on bonus slots no longer in effect, with the reason
-- 4. Delete inactive bonus slots that were never in a deal
--
-- Steps 3 and 4 are skipped if cybernations_db.nation is empty, so a failed
-- nation import can't end every bonus slot (as in V12)
-- =============================================================================
DROP PROCEDURE IF EXISTS `sync_team_event_slots`$$

CREATE PROCEDURE `sync_team_event_slots`(
    OUT p_deals_cancelled INT
)
MODIFIES SQL DATA
BEGIN
    DECLARE v_today DATE DEFAULT DATE(CONVERT_TZ(UTC_TIMESTAMP(), '+00:00', '-05:00'));

    SET p_deals_cancelled = 0;

    INSERT INTO slot (
        account_id,
        slot_number,
        is_team_bonus,
        team_event_id,
        tech_sent_cycles,
        tech_received_cycles,
        cash_sent_cycles,
        cash_received_cycles
    )
    SELECT
        a.id,
        7,
        TRUE,
        ate.team_event_id,
        CASE WHEN a.va IN ('S', 'F') THEN 1 ELSE 0 END,
        CASE WHEN a.va IN ('B', 'R') THEN 1 ELSE 0 END,
        CASE WHEN a.va IN ('B', 'D') THEN 1 ELSE 0 END,
        CASE WHEN a.va IN ('S', 'C') THEN 1 ELSE 0 END
    FROM account_team_event ate
    JOIN account a ON a.id = ate.account_id
    WHERE NOT EXISTS (
        SELECT 1 FROM slot s
        WHERE s.account_id = a.id AND s.is_team_bonus
    );

    -- account_team_event rather than team_bonus_slot from here on: MySQL
    -- won't change slot through a view that reads it
    UPDATE slot s
    JOIN account_team_event ate ON ate.account_id = s.account_id
    SET s.team_event_id = ate.team_event_id
    WHERE s.is_team_bonus
        AND NOT (s.team_event_id <=> ate.team_event_id);

    IF EXISTS (SELECT 1 FROM cybernations_db.nation) THEN
        UPDATE deal d
        JOIN slot s ON s.id IN (d.slot_1_id, d.slot_2_id)
        LEFT JOIN team_event te ON te.id = s.team_event_id
        SET
            d.status = 'cancelled',
            d.cancelled_on = CURRENT_TIMESTAMP,
            d.cancelled_by = 'system',
            d.cancellation_reason = CASE
                WHEN te.deleted_on IS NOT NULL THEN 'Team event removed by an admin'
                WHEN te.ends_on < v_today THEN 'Team event ended'
                WHEN te.id IS NOT NULL THEN CONCAT('Nation left the ',
                    ELT(te.team, 'Aqua', 'White', 'Red', 'Green', 'Brown', 'Blue', 'Purple', 'Orange',
                        'Maroon', 'Black', 'Yellow', 'Pink'), ' team')
                ELSE 'Team bonus slot ended'
            END
        WHERE s.is_team_bonus
            AND d.status = 'active'
            AND NOT EXISTS (SELECT 1 FROM account_team_event ate WHERE ate.account_id = s.account_id);

        SET p_deals_cancelled = ROW_COUNT();

        DELETE s
        FROM slot s
        LEFT JOIN account_team_event ate ON ate.account_id = s.account_id
        WHERE s.is_team_bonus
            AND ate.account_id IS NULL
            AND NOT EXISTS (
                SELECT 1 FROM deal d
                WHERE s.id IN (d.slot_1_id, d.slot_2_id)
            );
    END IF;
END$$

-- =============================================================================
-- PROCEDURE: match_deals_run
-- Same as V14, except a team bonus slot is only a candidate while it's in
-- effect and on or before its last_match_on (see team_bonus_slot)
-- =============================================================================
DROP PROCEDURE IF EXISTS `match_deals_run`$$

CREATE PROCEDURE `match_deals_run`(
    OUT p_deals_created INT
)
MODIFIES SQL DATA
BEGIN
    DECLARE v_batch_created INT DEFAULT 1;
    DECLARE v_iterations INT DEFAULT 0;
    DECLARE v_need_account INT;
    DECLARE v_done INT DEFAULT 0;
    DECLARE v_today DATE DEFAULT DATE(CONVERT_TZ(UTC_TIMESTAMP(), '+00:00', '-05:00'));
    DECLARE need_cur CURSOR FOR SELECT account_id FROM tmp_need;
    DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_done = 1;

    SET p_deals_created = 0;

    -- Same starting state the C# loop had after populate_wait_queue
    CALL populate_wait_queue();

    -- MySQL rule: a TEMPORARY table may be referenced only once per statement.
    -- Each statement below references each temp table at most once. Where two
    -- tables are needed side by side, a copy is kept (tmp_best_mirror).
    DROP TEMPORARY TABLE IF EXISTS tmp_pairs, tmp_accounts, tmp_best, tmp_need,
                                   tmp_best_mirror, tmp_matches, tmp_used_slot;

    -- Every compatible, eligible slot pair in both orientations, built once
    -- from base tables (base tables may be self-joined; temp tables may not)
    CREATE TEMPORARY TABLE tmp_pairs (
        slot_a_id        INT NOT NULL,
        slot_b_id        INT NOT NULL,
        account_a_id     INT NOT NULL,
        account_b_id     INT NOT NULL,
        slot_a_number    TINYINT NOT NULL,
        slot_b_number    TINYINT NOT NULL,
        partner_priority DECIMAL(10,2) NOT NULL,
        lo               INT NOT NULL,
        hi               INT NOT NULL,
        PRIMARY KEY (slot_a_id, slot_b_id),
        INDEX idx_rank (account_a_id, partner_priority DESC, account_b_id, slot_a_number, slot_b_number),
        INDEX idx_slot_b (slot_b_id),
        INDEX idx_pair (lo, hi)
    );

    INSERT INTO tmp_pairs (
        slot_a_id, slot_b_id, account_a_id, account_b_id,
        slot_a_number, slot_b_number, partner_priority, lo, hi
    )
    SELECT
        sa.id,
        sb.id,
        sa.account_id,
        sb.account_id,
        sa.slot_number,
        sb.slot_number,
        wqb.priority_score,
        LEAST(sa.account_id, sb.account_id),
        GREATEST(sa.account_id, sb.account_id)
    FROM wait_queue wqa
    JOIN slot sa ON sa.account_id = wqa.account_id
    JOIN slot sb ON
        sa.tech_sent_cycles = sb.tech_received_cycles AND
        sa.tech_received_cycles = sb.tech_sent_cycles AND
        sa.cash_sent_cycles = sb.cash_received_cycles AND
        sa.cash_received_cycles = sb.cash_sent_cycles
    JOIN wait_queue wqb ON sb.account_id = wqb.account_id
    WHERE sa.account_id != sb.account_id
        AND wqa.slots_needed > 0
        AND wqb.slots_needed > 0
        AND (sa.tech_sent_cycles + sa.tech_received_cycles + sa.cash_sent_cycles + sa.cash_received_cycles) > 0
        AND NOT EXISTS (
            SELECT 1 FROM deal d
            WHERE (d.slot_1_id = sa.id OR d.slot_2_id = sa.id)
                AND d.status = 'active'
        )
        AND NOT EXISTS (
            SELECT 1 FROM deal d
            WHERE (d.slot_1_id = sb.id OR d.slot_2_id = sb.id)
                AND d.status = 'active'
        )
        AND NOT EXISTS (
            SELECT 1 FROM deal_participants dp
            WHERE ((dp.account_1_id = sa.account_id AND dp.account_2_id = sb.account_id)
                OR (dp.account_1_id = sb.account_id AND dp.account_2_id = sa.account_id))
                AND dp.status = 'active'
        )
        -- Any status: unique_slot_pair allows only one deal per slot pair, ever
        AND NOT EXISTS (
            SELECT 1 FROM deal d
            WHERE d.slot_1_id = LEAST(sa.id, sb.id)
                AND d.slot_2_id = GREATEST(sa.id, sb.id)
        )
        -- Bonus slots only while their deal's legs can all be sent in time
        AND (NOT sa.is_team_bonus OR EXISTS (
            SELECT 1 FROM team_bonus_slot tbs
            WHERE tbs.slot_id = sa.id AND tbs.last_match_on >= v_today
        ))
        AND (NOT sb.is_team_bonus OR EXISTS (
            SELECT 1 FROM team_bonus_slot tbs
            WHERE tbs.slot_id = sb.id AND tbs.last_match_on >= v_today
        ));

    -- Accounts that still have at least one candidate pair
    CREATE TEMPORARY TABLE tmp_accounts (
        account_id INT NOT NULL PRIMARY KEY
    );
    INSERT INTO tmp_accounts (account_id)
    SELECT DISTINCT account_a_id FROM tmp_pairs;

    -- Each account's single best candidate, kept current incrementally
    CREATE TEMPORARY TABLE tmp_best (
        account_a_id INT NOT NULL PRIMARY KEY,
        account_b_id INT NOT NULL,
        slot_a_id    INT NOT NULL,
        slot_b_id    INT NOT NULL
    );

    CREATE TEMPORARY TABLE tmp_best_mirror (
        account_a_id INT NOT NULL PRIMARY KEY,
        account_b_id INT NOT NULL,
        slot_a_id    INT NOT NULL,
        slot_b_id    INT NOT NULL
    );

    -- Accounts whose best candidate must be (re)computed this iteration
    CREATE TEMPORARY TABLE tmp_need (
        account_id INT NOT NULL PRIMARY KEY
    );

    CREATE TEMPORARY TABLE tmp_used_slot (
        slot_id INT NOT NULL PRIMARY KEY
    );

    -- Same iteration cap and stop rule as the C# loop
    WHILE v_batch_created > 0 AND v_iterations < 500 DO
        SET v_iterations = v_iterations + 1;

        -- 1. Drop best rows whose pair was removed
        DELETE FROM tmp_best
        WHERE NOT EXISTS (
            SELECT 1 FROM tmp_pairs p
            WHERE p.slot_a_id = tmp_best.slot_a_id
                AND p.slot_b_id = tmp_best.slot_b_id
        );

        -- 2. Drop accounts with no candidate pairs left
        DELETE FROM tmp_accounts
        WHERE NOT EXISTS (
            SELECT 1 FROM tmp_pairs p
            WHERE p.account_a_id = tmp_accounts.account_id
        );

        -- 3. Accounts with no best row need one
        DELETE FROM tmp_need;
        INSERT INTO tmp_need (account_id)
        SELECT a.account_id
        FROM tmp_accounts a
        WHERE NOT EXISTS (
            SELECT 1 FROM tmp_best b WHERE b.account_a_id = a.account_id
        );

        -- 4. Recompute best only for those accounts: one index-ordered lookup each
        SET v_done = 0;
        OPEN need_cur;
        need_loop: LOOP
            FETCH need_cur INTO v_need_account;
            IF v_done = 1 THEN
                LEAVE need_loop;
            END IF;

            INSERT INTO tmp_best (account_a_id, account_b_id, slot_a_id, slot_b_id)
            SELECT account_a_id, account_b_id, slot_a_id, slot_b_id
            FROM tmp_pairs
            WHERE account_a_id = v_need_account
            ORDER BY partner_priority DESC, account_b_id, slot_a_number, slot_b_number
            LIMIT 1;
        END LOOP need_loop;
        CLOSE need_cur;

        -- 5. Mutual best: r1 and r2 are different temp tables
        DELETE FROM tmp_best_mirror;
        INSERT INTO tmp_best_mirror SELECT * FROM tmp_best;

        DROP TEMPORARY TABLE IF EXISTS tmp_matches;
        CREATE TEMPORARY TABLE tmp_matches AS
        SELECT
            LEAST(r1.slot_a_id, r1.slot_b_id)          AS slot_1_id,
            GREATEST(r1.slot_a_id, r1.slot_b_id)       AS slot_2_id,
            r1.account_a_id                             AS lo,
            r1.account_b_id                             AS hi
        FROM tmp_best r1
        JOIN tmp_best_mirror r2 ON r2.account_a_id = r1.account_b_id
            AND r2.account_b_id = r1.account_a_id
        WHERE r1.account_a_id < r1.account_b_id;

        INSERT INTO deal (slot_1_id, slot_2_id, created_by)
        SELECT slot_1_id, slot_2_id, 'system'
        FROM tmp_matches;

        SET v_batch_created = ROW_COUNT();
        SET p_deals_created = p_deals_created + v_batch_created;

        IF v_batch_created > 0 THEN
            -- Remove candidates that use a slot just filled, and any pair of
            -- accounts that now shares a deal. One reference per statement.
            DELETE FROM tmp_used_slot;
            INSERT INTO tmp_used_slot SELECT slot_1_id FROM tmp_matches;
            INSERT IGNORE INTO tmp_used_slot SELECT slot_2_id FROM tmp_matches;

            DELETE FROM tmp_pairs
            WHERE slot_a_id IN (SELECT slot_id FROM tmp_used_slot);

            DELETE FROM tmp_pairs
            WHERE slot_b_id IN (SELECT slot_id FROM tmp_used_slot);

            DELETE FROM tmp_pairs
            WHERE (lo, hi) IN (SELECT lo, hi FROM tmp_matches);
        END IF;
    END WHILE;

    -- Leave wait_queue consistent with the new deals, as before
    CALL populate_wait_queue();

    DROP TEMPORARY TABLE IF EXISTS tmp_pairs, tmp_accounts, tmp_best, tmp_need,
                                   tmp_best_mirror, tmp_matches, tmp_used_slot;
END$$

DELIMITER ;

-- =============================================================================
-- End of V17 Migration
-- =============================================================================
