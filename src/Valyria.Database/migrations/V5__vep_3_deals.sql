-- =============================================================================
-- V5__vep_3_deals.sql
-- VEP 3.0: Deal-Based System Migration
--
-- This migration introduces the deal-based system to replace the balance-ledger
-- approach. Key changes:
-- 1. New tables: deal, deal_activity, deal_flag, wait_queue
-- 2. New view: account_deal_summary (replaces balance view)
-- 3. New stored procedures: match_deals_daily, track_deal_activity, check_deal_inactivity
--
-- Migration Strategy: Hard cutover from balance system to deal system
-- No transition period - all accounts start fresh with zero deals
-- Historical balance/credit/debt data can be archived separately
-- =============================================================================

USE `vep_db`;

-- =============================================================================
-- TABLE: deal
-- Purpose: Bilateral aid exchange agreements between two accounts
--
-- Business Rules:
-- - Each deal involves exactly two accounts
-- - Direction: bidirectional (both send) or unidirectional (one sends)
-- - Compatible role pairs enforced at application layer:
--   * Buyer ↔ Seller (bidirectional)
--   * Cash Donor → Cash Collector (unidirectional)
--   * Tech Farm → Tech Receiver (unidirectional)
-- - Only one deal allowed per account pair (enforced by unique constraint)
-- - Each deal consumes 1 slot per participating account
-- - Perpetual until cancelled or role incompatibility
-- =============================================================================
CREATE TABLE IF NOT EXISTS `deal` (
    `id` INT AUTO_INCREMENT PRIMARY KEY,
    `account_1_id` INT NOT NULL,
    `account_2_id` INT NOT NULL,
    `direction` ENUM('bidirectional', 'unidirectional') NOT NULL,
    `status` ENUM('active', 'cancelled') NOT NULL DEFAULT 'active',
    `created_on` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `created_by` VARCHAR(50) NOT NULL COMMENT 'system or admin username',
    `cancelled_on` DATETIME NULL,
    `cancelled_by` VARCHAR(50) NULL,
    `cancellation_reason` TEXT NULL,

    FOREIGN KEY (`account_1_id`) REFERENCES `account`(`id`) ON DELETE CASCADE,
    FOREIGN KEY (`account_2_id`) REFERENCES `account`(`id`) ON DELETE CASCADE,

    -- Ensure only one deal per account pair using canonical ordering
    UNIQUE KEY `unique_partner_pair` (
        LEAST(`account_1_id`, `account_2_id`),
        GREATEST(`account_1_id`, `account_2_id`)
    ),

    INDEX `idx_account_1_active` (`account_1_id`, `status`),
    INDEX `idx_account_2_active` (`account_2_id`, `status`),
    INDEX `idx_status` (`status`),
    INDEX `idx_created_on` (`created_on`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Bilateral aid exchange agreements between accounts';

-- =============================================================================
-- TABLE: deal_activity
-- Purpose: Track aid sent/received for each deal on the 10-day schedule
--
-- Business Rules:
-- - One record per expected aid exchange per deal
-- - Links to actual aid record when aid is sent (aid_id)
-- - Tracks both sender and receiver perspectives
-- - Used to identify inactive deals (no matching aid within timeframe)
-- =============================================================================
CREATE TABLE IF NOT EXISTS `deal_activity` (
    `id` INT AUTO_INCREMENT PRIMARY KEY,
    `deal_id` INT NOT NULL,
    `aid_id` INT NULL COMMENT 'Links to cybernations_db.aid when aid sent',
    `expected_sender_nation_id` INT NOT NULL,
    `expected_receiver_nation_id` INT NOT NULL,
    `aid_sent` BOOLEAN NOT NULL DEFAULT FALSE,
    `aid_received` BOOLEAN NOT NULL DEFAULT FALSE,
    `activity_date` DATE NOT NULL COMMENT 'Date aid was expected/sent',
    `created_on` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    FOREIGN KEY (`deal_id`) REFERENCES `deal`(`id`) ON DELETE CASCADE,

    INDEX `idx_deal_date` (`deal_id`, `activity_date`),
    INDEX `idx_aid` (`aid_id`),
    INDEX `idx_activity_date` (`activity_date`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Activity tracking for deal aid exchanges';

-- =============================================================================
-- TABLE: deal_flag
-- Purpose: Track inactive or problematic deals requiring admin attention
--
-- Flag Types:
-- - 1_day_inactive: No matching aid record after 1 day
-- - 5_day_replacement: After 5 days, innocent party gets replacement deal
--
-- Business Rules:
-- - Flagged account: The offender (didn't send aid when expected)
-- - Innocent account: The receiver (if no aid sent) or sender (if aid pending)
-- - Offenders excluded from new deal assignments until resolved
-- =============================================================================
CREATE TABLE IF NOT EXISTS `deal_flag` (
    `id` INT AUTO_INCREMENT PRIMARY KEY,
    `deal_id` INT NOT NULL,
    `flag_type` ENUM('1_day_inactive', '5_day_replacement') NOT NULL,
    `flagged_account_id` INT NOT NULL COMMENT 'The offender',
    `innocent_account_id` INT NOT NULL COMMENT 'The innocent party',
    `flagged_on` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `resolved_on` DATETIME NULL,
    `resolved_by` VARCHAR(50) NULL COMMENT 'Admin username',
    `admin_notes` TEXT NULL,

    FOREIGN KEY (`deal_id`) REFERENCES `deal`(`id`) ON DELETE CASCADE,
    FOREIGN KEY (`flagged_account_id`) REFERENCES `account`(`id`) ON DELETE CASCADE,
    FOREIGN KEY (`innocent_account_id`) REFERENCES `account`(`id`) ON DELETE CASCADE,

    INDEX `idx_unresolved` (`resolved_on`, `flag_type`),
    INDEX `idx_flagged_account` (`flagged_account_id`),
    INDEX `idx_deal` (`deal_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Flags for inactive or problematic deals';

-- =============================================================================
-- TABLE: wait_queue
-- Purpose: Priority queue for accounts waiting to be matched with deals
--
-- Business Rules:
-- - Populated daily by match_deals_daily() procedure
-- - Priority score calculation:
--   * Primary: Accounts waiting longest without full deals
--   * Secondary: Accounts with aid slot opening soonest
-- - Cleared and rebuilt each day after matching
-- =============================================================================
CREATE TABLE IF NOT EXISTS `wait_queue` (
    `account_id` INT PRIMARY KEY,
    `slots_needed` INT NOT NULL COMMENT 'Number of additional deals needed',
    `entered_queue_on` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `next_slot_opens_on` DATE NULL COMMENT 'When current deal expires/slot becomes available',
    `priority_score` DECIMAL(10,2) NOT NULL COMMENT 'Higher = higher priority for matching',

    FOREIGN KEY (`account_id`) REFERENCES `account`(`id`) ON DELETE CASCADE,

    INDEX `idx_priority` (`priority_score` DESC)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Priority queue for deal matching';

-- =============================================================================
-- VIEW: account_deal_summary
-- Purpose: Replaces the 'balance' view with deal-based slot calculations
--
-- Provides per-account summary:
-- - slots_used: Count of active deals
-- - slots_available: Based on fm/dra flags (4-6 slots total)
-- - slots_full: Boolean indicating if all slots occupied
--
-- Note: Removed all credit/debt/balance calculations from VEP 2.5
-- =============================================================================
CREATE OR REPLACE VIEW `account_deal_summary` AS
SELECT
    a.id AS account_id,
    a.nation_id,
    a.va AS role,
    a.fm,
    a.dra,

    -- Calculate total slots available based on flags
    (4 + IF(a.fm = 1, 1, 0) + IF(a.dra = 1, 1, 0)) AS slots_total,

    -- Count active deals for this account
    COALESCE(
        (SELECT COUNT(*)
         FROM deal d
         WHERE (d.account_1_id = a.id OR d.account_2_id = a.id)
         AND d.status = 'active'),
        0
    ) AS slots_used,

    -- Calculate available slots
    (4 + IF(a.fm = 1, 1, 0) + IF(a.dra = 1, 1, 0)) - COALESCE(
        (SELECT COUNT(*)
         FROM deal d
         WHERE (d.account_1_id = a.id OR d.account_2_id = a.id)
         AND d.status = 'active'),
        0
    ) AS slots_available,

    -- Boolean: are all slots filled?
    CASE
        WHEN COALESCE(
            (SELECT COUNT(*)
             FROM deal d
             WHERE (d.account_1_id = a.id OR d.account_2_id = a.id)
             AND d.status = 'active'),
            0
        ) >= (4 + IF(a.fm = 1, 1, 0) + IF(a.dra = 1, 1, 0))
        THEN TRUE
        ELSE FALSE
    END AS slots_full

FROM account a;

-- =============================================================================
-- End of table and view definitions
-- Stored procedures defined below
-- =============================================================================

DELIMITER $$

-- =============================================================================
-- PROCEDURE: track_deal_activity
-- Purpose: Link newly imported aid records to existing deals and track activity
--
-- Flow:
-- 1. Find aid records not yet linked to deal_activity
-- 2. Match aid to deals based on sender/receiver nation IDs
-- 3. Create deal_activity records marking aid as sent/received
-- 4. Run after daily aid import (called by data loader)
--
-- Business Logic:
-- - For bidirectional deals: both parties send, so check both directions
-- - For unidirectional deals: only one party sends
-- - Links aid record to track what was actually exchanged
-- =============================================================================
CREATE PROCEDURE `track_deal_activity`()
MODIFIES SQL DATA
BEGIN
    -- Track aid sent for active deals
    -- This creates activity records linking aid to deals
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
        d.id AS deal_id,
        aid.id AS aid_id,
        aid.sending_nation_id AS expected_sender_nation_id,
        aid.receiving_nation_id AS expected_receiver_nation_id,
        TRUE AS aid_sent,
        TRUE AS aid_received,
        DATE(aid.date) AS activity_date
    FROM cybernations_db.aid aid
    JOIN account sender_acct ON aid.sending_nation_id = sender_acct.nation_id
    JOIN account receiver_acct ON aid.receiving_nation_id = receiver_acct.nation_id
    JOIN deal d ON (
        (d.account_1_id = sender_acct.id AND d.account_2_id = receiver_acct.id) OR
        (d.account_2_id = sender_acct.id AND d.account_1_id = receiver_acct.id)
    )
    LEFT JOIN deal_activity da ON aid.id = da.aid_id
    WHERE d.status = 'active'
        AND da.id IS NULL  -- Not already tracked
        AND aid.date >= DATE_SUB(CURDATE(), INTERVAL 30 DAY);  -- Only recent aid
END$$

-- =============================================================================
-- PROCEDURE: check_deal_inactivity
-- Purpose: Flag inactive deals and create replacement deals for innocent parties
--
-- Flow:
-- 1. Identify deals with no activity in past 1 day (flag warning)
-- 2. Identify deals with no activity in past 5 days (flag + replace)
-- 3. For 5-day flags: create new deal for innocent party
-- 4. For 5-day flags: exclude offender from new deal assignments
--
-- Business Logic:
-- - 1 day: Flag deal as potentially inactive (warning only)
-- - 5 days: Flag deal for replacement, find new partner for innocent party
-- - Innocent party: Receiver (if no aid sent) or sender (if aid pending)
-- - Offender: The account that failed to send expected aid
-- =============================================================================
CREATE PROCEDURE `check_deal_inactivity`()
MODIFIES SQL DATA
BEGIN
    DECLARE done INT DEFAULT FALSE;
    DECLARE v_deal_id INT;
    DECLARE v_flagged_account_id INT;
    DECLARE v_innocent_account_id INT;

    -- Cursor for 5-day inactive deals needing replacement
    DECLARE inactive_cursor CURSOR FOR
        SELECT
            d.id AS deal_id,
            -- Determine who is the offender (expected sender who didn't send)
            -- For simplicity, we'll flag account_1 if no recent activity from them
            -- This logic would be refined based on deal direction and activity tracking
            d.account_1_id AS flagged_account_id,
            d.account_2_id AS innocent_account_id
        FROM deal d
        WHERE d.status = 'active'
            AND NOT EXISTS (
                SELECT 1
                FROM deal_activity da
                WHERE da.deal_id = d.id
                    AND da.activity_date >= DATE_SUB(CURDATE(), INTERVAL 5 DAY)
            )
            AND NOT EXISTS (
                SELECT 1
                FROM deal_flag df
                WHERE df.deal_id = d.id
                    AND df.flag_type = '5_day_replacement'
                    AND df.resolved_on IS NULL
            );

    DECLARE CONTINUE HANDLER FOR NOT FOUND SET done = TRUE;

    -- Step 1: Flag deals with 1 day of inactivity (warning)
    INSERT INTO deal_flag (
        deal_id,
        flag_type,
        flagged_account_id,
        innocent_account_id
    )
    SELECT
        d.id AS deal_id,
        '1_day_inactive' AS flag_type,
        d.account_1_id AS flagged_account_id,
        d.account_2_id AS innocent_account_id
    FROM deal d
    WHERE d.status = 'active'
        AND NOT EXISTS (
            SELECT 1
            FROM deal_activity da
            WHERE da.deal_id = d.id
                AND da.activity_date >= DATE_SUB(CURDATE(), INTERVAL 1 DAY)
        )
        AND NOT EXISTS (
            SELECT 1
            FROM deal_flag df
            WHERE df.deal_id = d.id
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
        d.id AS deal_id,
        '5_day_replacement' AS flag_type,
        d.account_1_id AS flagged_account_id,
        d.account_2_id AS innocent_account_id
    FROM deal d
    WHERE d.status = 'active'
        AND NOT EXISTS (
            SELECT 1
            FROM deal_activity da
            WHERE da.deal_id = d.id
                AND da.activity_date >= DATE_SUB(CURDATE(), INTERVAL 5 DAY)
        )
        AND NOT EXISTS (
            SELECT 1
            FROM deal_flag df
            WHERE df.deal_id = d.id
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

    -- Step 4: Add innocent parties to wait queue for replacement matching
    -- This will be handled by match_deals_daily() which checks for unfilled slots
    -- No explicit action needed here - the cancelled deal frees up a slot
END$$

-- =============================================================================
-- PROCEDURE: match_deals_daily
-- Purpose: Daily deal matching to fill empty slots
--
-- Flow:
-- 1. Clear and rebuild wait_queue with accounts needing deals
-- 2. Calculate priority scores (wait time + slot opening timing)
-- 3. Match compatible accounts based on role compatibility
-- 4. Create new deals for matched pairs
-- 5. Repeat until no more matches possible
--
-- Priority Calculation:
-- - Primary: Days since account joined without full deals (fairness)
-- - Secondary: When next slot opens (efficiency)
-- - Excludes: Accounts flagged as offenders in unresolved deal_flags
--
-- Compatibility Rules:
-- - Buyer (B) ↔ Seller (S): bidirectional
-- - Cash Donor (D) → Cash Collector (C): unidirectional
-- - Tech Farm (F) → Tech Receiver (R): unidirectional
-- - No duplicate pairings (enforced by unique constraint on deal table)
-- =============================================================================
CREATE PROCEDURE `match_deals_daily`()
MODIFIES SQL DATA
BEGIN
    DECLARE done INT DEFAULT FALSE;
    DECLARE v_account_1_id INT;
    DECLARE v_account_1_role CHAR(1);
    DECLARE v_account_2_id INT;
    DECLARE v_account_2_role CHAR(1);
    DECLARE v_direction VARCHAR(20);

    -- Step 1: Rebuild wait queue with accounts needing deals
    TRUNCATE TABLE wait_queue;

    INSERT INTO wait_queue (
        account_id,
        slots_needed,
        entered_queue_on,
        next_slot_opens_on,
        priority_score
    )
    SELECT
        ads.account_id,
        ads.slots_available AS slots_needed,
        COALESCE(a.date_added, CURRENT_TIMESTAMP) AS entered_queue_on,
        NULL AS next_slot_opens_on,  -- Could be enhanced to track slot opening
        -- Priority: days waiting (100 weight) + normalized slots needed (10 weight)
        DATEDIFF(CURDATE(), COALESCE(a.date_added, CURDATE())) * 100 +
        (ads.slots_available * 10) AS priority_score
    FROM account_deal_summary ads
    JOIN account a ON ads.account_id = a.id
    WHERE ads.slots_available > 0
        AND a.va NOT IN ('H')  -- Exclude inactive accounts
        -- Exclude flagged offenders
        AND NOT EXISTS (
            SELECT 1
            FROM deal_flag df
            WHERE df.flagged_account_id = ads.account_id
                AND df.resolved_on IS NULL
        );

    -- Step 2: Match accounts and create deals
    -- We'll use a simplified matching algorithm for now
    -- Match Buyers with Sellers (bidirectional)
    INSERT INTO deal (
        account_1_id,
        account_2_id,
        direction,
        created_by
    )
    SELECT
        buyer.account_id AS account_1_id,
        seller.account_id AS account_2_id,
        'bidirectional' AS direction,
        'system' AS created_by
    FROM wait_queue buyer
    JOIN account buyer_acct ON buyer.account_id = buyer_acct.id
    JOIN wait_queue seller
    JOIN account seller_acct ON seller.account_id = seller_acct.id
    WHERE buyer_acct.va = 'B'  -- Buyer
        AND seller_acct.va = 'S'  -- Seller
        AND buyer.account_id < seller.account_id  -- Prevent duplicates
        AND NOT EXISTS (
            SELECT 1
            FROM deal existing
            WHERE (
                (existing.account_1_id = buyer.account_id AND existing.account_2_id = seller.account_id) OR
                (existing.account_1_id = seller.account_id AND existing.account_2_id = buyer.account_id)
            )
        )
    ORDER BY buyer.priority_score DESC, seller.priority_score DESC
    LIMIT 100;  -- Process in batches

    -- Match Cash Donors with Cash Collectors (unidirectional)
    INSERT INTO deal (
        account_1_id,
        account_2_id,
        direction,
        created_by
    )
    SELECT
        donor.account_id AS account_1_id,
        collector.account_id AS account_2_id,
        'unidirectional' AS direction,
        'system' AS created_by
    FROM wait_queue donor
    JOIN account donor_acct ON donor.account_id = donor_acct.id
    JOIN wait_queue collector
    JOIN account collector_acct ON collector.account_id = collector_acct.id
    WHERE donor_acct.va = 'D'  -- Cash Donor
        AND collector_acct.va = 'C'  -- Cash Collector
        AND donor.account_id < collector.account_id
        AND NOT EXISTS (
            SELECT 1
            FROM deal existing
            WHERE (
                (existing.account_1_id = donor.account_id AND existing.account_2_id = collector.account_id) OR
                (existing.account_1_id = collector.account_id AND existing.account_2_id = donor.account_id)
            )
        )
    ORDER BY donor.priority_score DESC, collector.priority_score DESC
    LIMIT 100;

    -- Match Tech Farms with Tech Receivers (unidirectional)
    INSERT INTO deal (
        account_1_id,
        account_2_id,
        direction,
        created_by
    )
    SELECT
        farm.account_id AS account_1_id,
        receiver.account_id AS account_2_id,
        'unidirectional' AS direction,
        'system' AS created_by
    FROM wait_queue farm
    JOIN account farm_acct ON farm.account_id = farm_acct.id
    JOIN wait_queue receiver
    JOIN account receiver_acct ON receiver.account_id = receiver_acct.id
    WHERE farm_acct.va = 'F'  -- Tech Farm
        AND receiver_acct.va = 'R'  -- Tech Receiver
        AND farm.account_id < receiver.account_id
        AND NOT EXISTS (
            SELECT 1
            FROM deal existing
            WHERE (
                (existing.account_1_id = farm.account_id AND existing.account_2_id = receiver.account_id) OR
                (existing.account_1_id = receiver.account_id AND existing.account_2_id = farm.account_id)
            )
        )
    ORDER BY farm.priority_score DESC, receiver.priority_score DESC
    LIMIT 100;

    -- Step 3: Clean up wait queue after matching
    -- (Optional - could keep for auditing, or truncate for clean state)
END$$

DELIMITER ;

-- =============================================================================
-- End of V5 Migration
-- =============================================================================
