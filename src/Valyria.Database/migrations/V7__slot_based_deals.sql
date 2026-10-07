-- =============================================================================
-- V7__slot_based_deals.sql
-- VEP 3.0 Phase 3.5: Slot-Based Deal Architecture
--
-- Restructures deals from account-role matching to slot matching:
-- 1. New table: slot (4-6 per account, each with configurable cycle values)
-- 2. deal references two slots instead of two accounts (direction removed)
-- 3. New view: deal_participants (deal -> account pair, for activity tracking)
-- 4. Updated view: account_deal_summary (slots used = slots in active deals)
-- 5. New procedures: generate_slots_for_account, regenerate_slots_for_account
-- 6. Updated procedures: match_deals_batch, track_deal_activity, check_deal_inactivity
-- 7. Dropped procedure: match_deals_daily (superseded by V6 batch matching)
-- 8. New triggers: generate/regenerate slots on account insert/update
--
-- Migration Strategy: VEP 3.0 is not live, so all existing deals (and their
-- activity/flags, via cascade) are wiped. Slots are generated for every
-- account and the wait queue is rebuilt. Run match_deals_batch in a loop
-- afterwards (the data loader does this) to create the initial deals.
-- =============================================================================

USE `vep_db`;

-- =============================================================================
-- Step 1: Wipe existing deals (deal_activity and deal_flag cascade)
-- Must happen before the deal table is restructured
-- =============================================================================
DELETE FROM `deal`;
TRUNCATE TABLE `wait_queue`;

-- =============================================================================
-- TABLE: slot
-- Purpose: A single deal "seat" belonging to an account
--
-- Business Rules:
-- - Each account has 4 slots, +1 if fm, +1 if dra (max 6)
-- - Slots are generated/removed automatically by triggers; admins may only
--   edit cycle values
-- - Cycle values are aid sent/received per 10-day period; no validation
-- - Two slots match when their cycle values are exact inverses
-- =============================================================================
CREATE TABLE IF NOT EXISTS `slot` (
    `id` INT AUTO_INCREMENT PRIMARY KEY,
    `account_id` INT NOT NULL,
    `slot_number` TINYINT NOT NULL COMMENT '1-6',
    `tech_sent_cycles` INT NOT NULL DEFAULT 0,
    `tech_received_cycles` INT NOT NULL DEFAULT 0,
    `cash_sent_cycles` INT NOT NULL DEFAULT 0,
    `cash_received_cycles` INT NOT NULL DEFAULT 0,
    `created_on` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `updated_on` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    `updated_by` VARCHAR(50) NULL COMMENT 'admin username if manually edited',

    FOREIGN KEY (`account_id`) REFERENCES `account`(`id`) ON DELETE CASCADE,

    UNIQUE KEY `unique_account_slot` (`account_id`, `slot_number`),
    CONSTRAINT `chk_slot_number` CHECK (`slot_number` BETWEEN 1 AND 6),

    INDEX `idx_account` (`account_id`),
    INDEX `idx_cycles` (`tech_sent_cycles`, `cash_received_cycles`, `tech_received_cycles`, `cash_sent_cycles`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Deal slots per account with configurable aid cycles';

-- =============================================================================
-- TABLE: deal (restructure)
-- Replace account_1_id/account_2_id/direction with slot_1_id/slot_2_id
-- =============================================================================
ALTER TABLE `deal`
    DROP FOREIGN KEY `deal_ibfk_1`,
    DROP FOREIGN KEY `deal_ibfk_2`,
    DROP CHECK `chk_account_order`;

ALTER TABLE `deal`
    DROP INDEX `unique_partner_pair`,
    DROP INDEX `idx_account_1_active`,
    DROP INDEX `idx_account_2_active`,
    DROP COLUMN `account_1_id`,
    DROP COLUMN `account_2_id`,
    DROP COLUMN `direction`;

ALTER TABLE `deal`
    ADD COLUMN `slot_1_id` INT NOT NULL AFTER `id`,
    ADD COLUMN `slot_2_id` INT NOT NULL AFTER `slot_1_id`,
    ADD CONSTRAINT `fk_deal_slot_1` FOREIGN KEY (`slot_1_id`) REFERENCES `slot`(`id`) ON DELETE CASCADE,
    ADD CONSTRAINT `fk_deal_slot_2` FOREIGN KEY (`slot_2_id`) REFERENCES `slot`(`id`) ON DELETE CASCADE,
    -- Application layer enforces slot_1_id < slot_2_id before insertion
    ADD CONSTRAINT `chk_slot_order` CHECK (`slot_1_id` < `slot_2_id`),
    ADD UNIQUE KEY `unique_slot_pair` (`slot_1_id`, `slot_2_id`),
    ADD INDEX `idx_slot_2` (`slot_2_id`);

-- =============================================================================
-- VIEW: deal_participants
-- Purpose: Resolve each deal's slots to the owning accounts
-- =============================================================================
CREATE OR REPLACE VIEW `deal_participants` AS
SELECT
    d.id AS deal_id,
    d.status,
    d.slot_1_id,
    d.slot_2_id,
    s1.account_id AS account_1_id,
    s2.account_id AS account_2_id
FROM deal d
JOIN slot s1 ON d.slot_1_id = s1.id
JOIN slot s2 ON d.slot_2_id = s2.id;

-- =============================================================================
-- VIEW: account_deal_summary
-- Purpose: Per-account slot usage (same columns as V5)
-- - slots_total: 4-6 based on fm/dra flags
-- - slots_used: account slots that are part of an active deal
-- =============================================================================
CREATE OR REPLACE VIEW `account_deal_summary` AS
SELECT
    a.id AS account_id,
    a.nation_id,
    a.va AS role,
    a.fm,
    a.dra,
    (4 + IF(a.fm = 1, 1, 0) + IF(a.dra = 1, 1, 0)) AS slots_total,
    COALESCE(u.slots_used, 0) AS slots_used,
    GREATEST((4 + IF(a.fm = 1, 1, 0) + IF(a.dra = 1, 1, 0)) - COALESCE(u.slots_used, 0), 0) AS slots_available,
    CASE
        WHEN COALESCE(u.slots_used, 0) >= (4 + IF(a.fm = 1, 1, 0) + IF(a.dra = 1, 1, 0)) THEN TRUE
        ELSE FALSE
    END AS slots_full
FROM account a
LEFT JOIN (
    SELECT s.account_id, COUNT(DISTINCT s.id) AS slots_used
    FROM slot s
    JOIN deal d ON (d.slot_1_id = s.id OR d.slot_2_id = s.id)
    WHERE d.status = 'active'
    GROUP BY s.account_id
) u ON u.account_id = a.id;

DELIMITER $$

-- =============================================================================
-- PROCEDURE: generate_slots_for_account
-- Purpose: Create any missing slots for an account using role defaults
--
-- Parameters:
--   p_account_id - Account to generate slots for (NULL = every account)
--
-- Role defaults (tech_sent, tech_received, cash_sent, cash_received):
--   S Seller          1, 0, 0, 1
--   B Buyer           0, 1, 1, 0
--   D Cash Donor      0, 0, 1, 0
--   C Cash Collector  0, 0, 0, 1
--   F Tech Farm       1, 0, 0, 0
--   R Tech Receiver   0, 1, 0, 0
--   Anything else     0, 0, 0, 0 (never matched)
-- =============================================================================
DROP PROCEDURE IF EXISTS `generate_slots_for_account`$$

CREATE PROCEDURE `generate_slots_for_account`(
    IN p_account_id INT
)
MODIFIES SQL DATA
BEGIN
    INSERT INTO slot (
        account_id,
        slot_number,
        tech_sent_cycles,
        tech_received_cycles,
        cash_sent_cycles,
        cash_received_cycles
    )
    SELECT
        a.id,
        n.slot_number,
        CASE WHEN a.va IN ('S', 'F') THEN 1 ELSE 0 END,
        CASE WHEN a.va IN ('B', 'R') THEN 1 ELSE 0 END,
        CASE WHEN a.va IN ('B', 'D') THEN 1 ELSE 0 END,
        CASE WHEN a.va IN ('S', 'C') THEN 1 ELSE 0 END
    FROM account a
    CROSS JOIN (
        SELECT 1 AS slot_number UNION ALL SELECT 2 UNION ALL SELECT 3
        UNION ALL SELECT 4 UNION ALL SELECT 5 UNION ALL SELECT 6
    ) n
    WHERE (p_account_id IS NULL OR a.id = p_account_id)
        AND n.slot_number <= (4 + IF(a.fm = 1, 1, 0) + IF(a.dra = 1, 1, 0))
        AND NOT EXISTS (
            SELECT 1 FROM slot s
            WHERE s.account_id = a.id AND s.slot_number = n.slot_number
        );
END$$

-- =============================================================================
-- PROCEDURE: regenerate_slots_for_account
-- Purpose: Bring an account's slots in line with its current fm/dra/role
--
-- Parameters:
--   p_account_id   - Account to regenerate slots for
--   p_role_changed - TRUE if the account's role (va) changed
--
-- Cases:
-- - Role changed: cancel the account's active deals (no longer compatible)
--   and reset every slot to the new role defaults (admin edits are lost)
-- - Fewer slots needed: cancel deals on the extra slots, then delete them
--   (note: deleting a slot cascades to its deals and their activity/flags)
-- - More slots needed: add the missing slots with role defaults
-- =============================================================================
DROP PROCEDURE IF EXISTS `regenerate_slots_for_account`$$

CREATE PROCEDURE `regenerate_slots_for_account`(
    IN p_account_id INT,
    IN p_role_changed BOOLEAN
)
MODIFIES SQL DATA
BEGIN
    DECLARE v_slot_count INT;

    SELECT (4 + IF(fm = 1, 1, 0) + IF(dra = 1, 1, 0))
    INTO v_slot_count
    FROM account
    WHERE id = p_account_id;

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
        AND d.status = 'active';

    DELETE FROM slot
    WHERE account_id = p_account_id
        AND slot_number > v_slot_count;

    -- Add missing slots (fm/dra turned on)
    CALL generate_slots_for_account(p_account_id);
END$$

-- =============================================================================
-- PROCEDURE: match_deals_batch
-- Purpose: Process one batch of slot-based deal matches
--
-- This procedure should be called repeatedly until it returns 0 deals created.
--
-- Parameters:
--   OUT p_deals_created - Number of deals created in this batch
--
-- Compatibility: two free slots match when their cycles are exact inverses
--   a.tech_sent = b.tech_received, a.tech_received = b.tech_sent,
--   a.cash_sent = b.cash_received, a.cash_received = b.cash_sent
--
-- Algorithm:
-- 1. Build every compatible (account A slot, account B slot) candidate, in
--    both orientations, excluding:
--    - slots already in an active deal
--    - all-zero slots (they are trivially "compatible" with each other)
--    - slots on the same account
--    - account pairs that already share an active deal (one deal per pair)
-- 2. Each account picks its best candidate (highest partner priority, ties
--    broken deterministically) via ROW_NUMBER() partitioned by account
-- 3. Create deals only where two accounts picked each other, so each
--    account (and therefore each slot) gets at most one deal per batch.
--    The highest-priority account and its best partner always pick each
--    other, so every batch with candidates creates at least one deal.
-- 4. Refresh wait_queue to reflect new deals
-- =============================================================================
DROP PROCEDURE IF EXISTS `match_deals_batch`$$

CREATE PROCEDURE `match_deals_batch`(
    OUT p_deals_created INT
)
MODIFIES SQL DATA
BEGIN
    SET p_deals_created = 0;

    DROP TEMPORARY TABLE IF EXISTS temp_slot_matches;
    CREATE TEMPORARY TABLE temp_slot_matches AS
    WITH ranked AS (
        SELECT
            sa.account_id AS account_a_id,
            sb.account_id AS account_b_id,
            sa.id AS slot_a_id,
            sb.id AS slot_b_id,
            ROW_NUMBER() OVER (
                PARTITION BY sa.account_id
                ORDER BY wqb.priority_score DESC, sb.account_id, sa.slot_number, sb.slot_number
            ) AS rn
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
    )
    SELECT
        LEAST(r1.slot_a_id, r1.slot_b_id) AS slot_1_id,
        GREATEST(r1.slot_a_id, r1.slot_b_id) AS slot_2_id
    FROM ranked r1
    JOIN ranked r2 ON r2.account_a_id = r1.account_b_id
        AND r2.account_b_id = r1.account_a_id
    WHERE r1.rn = 1
        AND r2.rn = 1
        AND r1.account_a_id < r1.account_b_id;

    INSERT INTO deal (slot_1_id, slot_2_id, created_by)
    SELECT slot_1_id, slot_2_id, 'system'
    FROM temp_slot_matches;

    SET p_deals_created = ROW_COUNT();

    DROP TEMPORARY TABLE IF EXISTS temp_slot_matches;

    -- Refresh wait_queue to reflect new deals
    CALL populate_wait_queue();
END$$

-- =============================================================================
-- PROCEDURE: track_deal_activity
-- Purpose: Link newly imported aid records to existing deals
-- Unchanged from V5 except deals resolve to accounts through slots
-- =============================================================================
DROP PROCEDURE IF EXISTS `track_deal_activity`$$

CREATE PROCEDURE `track_deal_activity`()
MODIFIES SQL DATA
BEGIN
    INSERT INTO deal_activity (
        deal_id,
        aid_id,
        expected_sender_nation_id,
        expected_receiver_nation_id,
        aid_sent,
        aid_received,
        activity_date
    )
    SELECT DISTINCT
        dp.deal_id AS deal_id,
        aid.id AS aid_id,
        aid.sending_nation_id AS expected_sender_nation_id,
        aid.receiving_nation_id AS expected_receiver_nation_id,
        TRUE AS aid_sent,
        TRUE AS aid_received,
        DATE(aid.date) AS activity_date
    FROM cybernations_db.aid aid
    JOIN account sender_acct ON aid.sending_nation_id = sender_acct.nation_id
    JOIN account receiver_acct ON aid.receiving_nation_id = receiver_acct.nation_id
    JOIN deal_participants dp ON (
        (dp.account_1_id = sender_acct.id AND dp.account_2_id = receiver_acct.id) OR
        (dp.account_2_id = sender_acct.id AND dp.account_1_id = receiver_acct.id)
    )
    LEFT JOIN deal_activity da ON aid.id = da.aid_id
    WHERE dp.status = 'active'
        AND da.id IS NULL  -- Not already tracked
        AND aid.date >= DATE_SUB(CURDATE(), INTERVAL 30 DAY);  -- Only recent aid
END$$

-- =============================================================================
-- PROCEDURE: check_deal_inactivity
-- Purpose: Flag inactive deals and cancel 5-day inactive deals
-- Unchanged from V5 except deals resolve to accounts through slots
-- =============================================================================
DROP PROCEDURE IF EXISTS `check_deal_inactivity`$$

CREATE PROCEDURE `check_deal_inactivity`()
MODIFIES SQL DATA
BEGIN
    -- Step 1: Flag deals with 1 day of inactivity (warning)
    INSERT INTO deal_flag (
        deal_id,
        flag_type,
        flagged_account_id,
        innocent_account_id
    )
    SELECT
        dp.deal_id AS deal_id,
        '1_day_inactive' AS flag_type,
        dp.account_1_id AS flagged_account_id,
        dp.account_2_id AS innocent_account_id
    FROM deal_participants dp
    WHERE dp.status = 'active'
        AND NOT EXISTS (
            SELECT 1
            FROM deal_activity da
            WHERE da.deal_id = dp.deal_id
                AND da.activity_date >= DATE_SUB(CURDATE(), INTERVAL 1 DAY)
        )
        AND NOT EXISTS (
            SELECT 1
            FROM deal_flag df
            WHERE df.deal_id = dp.deal_id
                AND df.flag_type = '1_day_inactive'
                AND df.resolved_on IS NULL
        );

    -- Step 2: Flag deals with 5 days of inactivity (replacement needed)
    INSERT INTO deal_flag (
        deal_id,
        flag_type,
        flagged_account_id,
        innocent_account_id
    )
    SELECT
        dp.deal_id AS deal_id,
        '5_day_replacement' AS flag_type,
        dp.account_1_id AS flagged_account_id,
        dp.account_2_id AS innocent_account_id
    FROM deal_participants dp
    WHERE dp.status = 'active'
        AND NOT EXISTS (
            SELECT 1
            FROM deal_activity da
            WHERE da.deal_id = dp.deal_id
                AND da.activity_date >= DATE_SUB(CURDATE(), INTERVAL 5 DAY)
        )
        AND NOT EXISTS (
            SELECT 1
            FROM deal_flag df
            WHERE df.deal_id = dp.deal_id
                AND df.flag_type = '5_day_replacement'
                AND df.resolved_on IS NULL
        );

    -- Step 3: Cancel flagged deals (5-day inactive)
    UPDATE deal d
    JOIN deal_flag df ON d.id = df.deal_id
    SET
        d.status = 'cancelled',
        d.cancelled_on = CURRENT_TIMESTAMP,
        d.cancelled_by = 'system',
        d.cancellation_reason = 'Inactive for 5+ days - automatically cancelled'
    WHERE df.flag_type = '5_day_replacement'
        AND df.resolved_on IS NULL
        AND d.status = 'active';

    -- Step 4: The cancelled deal frees up a slot; match_deals_batch refills it
END$$

-- Superseded by populate_wait_queue + match_deals_batch (V6) and references
-- the removed account columns
DROP PROCEDURE IF EXISTS `match_deals_daily`$$

-- =============================================================================
-- TRIGGERS: keep slots in sync with account fm/dra/role
-- =============================================================================
DROP TRIGGER IF EXISTS `after_account_insert_generate_slots`$$

CREATE TRIGGER `after_account_insert_generate_slots`
AFTER INSERT ON `account`
FOR EACH ROW
BEGIN
    CALL generate_slots_for_account(NEW.id);
END$$

DROP TRIGGER IF EXISTS `after_account_update_regenerate_slots`$$

CREATE TRIGGER `after_account_update_regenerate_slots`
AFTER UPDATE ON `account`
FOR EACH ROW
BEGIN
    IF (OLD.fm != NEW.fm OR OLD.dra != NEW.dra OR OLD.va != NEW.va) THEN
        CALL regenerate_slots_for_account(NEW.id, OLD.va != NEW.va);
    END IF;
END$$

DELIMITER ;

-- =============================================================================
-- Step 2: Generate slots for all existing accounts and rebuild wait queue
-- =============================================================================
CALL generate_slots_for_account(NULL);
CALL populate_wait_queue();

-- =============================================================================
-- End of V7 Migration
-- =============================================================================
