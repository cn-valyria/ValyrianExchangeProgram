-- =============================================================================
-- V6__fix_match_deals_daily.sql
-- VEP 3.0: Fix match_deals_daily to respect slot limits
--
-- Bug Fix:
-- The original match_deals_daily procedure created excessive deals because it:
-- 1. Didn't enforce slot limits during matching
-- 2. Didn't re-check available slots after creating deals
-- 3. Used bulk INSERTs that could match one account multiple times
--
-- Solution:
-- - Split into two procedures: setup and batch matching
-- - populate_wait_queue: Builds the queue once per day
-- - match_deals_batch: Processes a batch of matches (called in loop from C#)
-- - Use set-based operations with ROW_NUMBER() to prevent duplicate matching
-- - C# calls match_deals_batch repeatedly until it returns 0
-- =============================================================================

USE `vep_db`;

DELIMITER $$

-- =============================================================================
-- PROCEDURE: populate_wait_queue
-- Purpose: Rebuild wait queue with accounts needing deals (called once per day)
--
-- This procedure should be called first, before any batch matching begins.
-- It clears and rebuilds the wait_queue with all accounts that have available slots.
-- =============================================================================
DROP PROCEDURE IF EXISTS `populate_wait_queue`$$

CREATE PROCEDURE `populate_wait_queue`()
MODIFIES SQL DATA
BEGIN
    -- Rebuild wait queue with accounts needing deals
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
        NULL AS next_slot_opens_on,
        -- Priority: days waiting (100 weight) + slots needed (10 weight)
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
END$$

-- =============================================================================
-- PROCEDURE: match_deals_batch
-- Purpose: Process one batch of deal matches
--
-- This procedure should be called repeatedly until it returns 0 deals created.
-- It processes matches in batches using set-based operations for performance.
--
-- Parameters:
--   OUT p_deals_created - Number of deals created in this batch
--
-- Algorithm:
-- 1. For each role pairing (B-S, D-C, F-R):
--    a. Use ROW_NUMBER() to assign unique pairs (one match per account)
--    b. Create deals for all compatible pairs in wait_queue
-- 2. Refresh wait_queue to reflect new deals
-- 3. Set p_deals_created output parameter
-- 4. C# calls again if result > 0
-- =============================================================================
DROP PROCEDURE IF EXISTS `match_deals_batch`$$

CREATE PROCEDURE `match_deals_batch`(
    OUT p_deals_created INT
)
MODIFIES SQL DATA
BEGIN
    SET p_deals_created = 0;

    -- =================================================================
    -- Phase 1: Match Buyers with Sellers (bidirectional)
    -- Create temp table inline with matches
    -- =================================================================
    DROP TEMPORARY TABLE IF EXISTS temp_bs_matches;
    CREATE TEMPORARY TABLE temp_bs_matches AS
    SELECT
        LEAST(buyer_id, seller_id) AS account_1_id,
        GREATEST(buyer_id, seller_id) AS account_2_id,
        'bidirectional' AS direction
    FROM (
        SELECT
            buyer.account_id AS buyer_id,
            seller.account_id AS seller_id,
            ROW_NUMBER() OVER (PARTITION BY buyer.account_id ORDER BY seller.priority_score DESC) AS buyer_rn,
            ROW_NUMBER() OVER (PARTITION BY seller.account_id ORDER BY buyer.priority_score DESC) AS seller_rn
        FROM wait_queue buyer
        JOIN account buyer_acct ON buyer.account_id = buyer_acct.id
        CROSS JOIN wait_queue seller
        JOIN account seller_acct ON seller.account_id = seller_acct.id
        WHERE buyer_acct.va = 'B'
            AND seller_acct.va = 'S'
            AND buyer.slots_needed > 0
            AND seller.slots_needed > 0
            AND buyer.account_id != seller.account_id
            AND NOT EXISTS (
                SELECT 1 FROM deal d
                WHERE ((d.account_1_id = buyer.account_id AND d.account_2_id = seller.account_id)
                    OR (d.account_1_id = seller.account_id AND d.account_2_id = buyer.account_id))
                AND d.status = 'active'
            )
    ) ranked
    WHERE buyer_rn = 1 AND seller_rn = 1;

    -- =================================================================
    -- Phase 2: Match Cash Donors with Cash Collectors (unidirectional)
    -- Create temp table inline with matches
    -- =================================================================
    DROP TEMPORARY TABLE IF EXISTS temp_dc_matches;
    CREATE TEMPORARY TABLE temp_dc_matches AS
    SELECT
        LEAST(donor_id, collector_id) AS account_1_id,
        GREATEST(donor_id, collector_id) AS account_2_id,
        'unidirectional' AS direction
    FROM (
        SELECT
            donor.account_id AS donor_id,
            collector.account_id AS collector_id,
            ROW_NUMBER() OVER (PARTITION BY donor.account_id ORDER BY collector.priority_score DESC) AS donor_rn,
            ROW_NUMBER() OVER (PARTITION BY collector.account_id ORDER BY donor.priority_score DESC) AS collector_rn
        FROM wait_queue donor
        JOIN account donor_acct ON donor.account_id = donor_acct.id
        CROSS JOIN wait_queue collector
        JOIN account collector_acct ON collector.account_id = collector_acct.id
        WHERE donor_acct.va = 'D'
            AND collector_acct.va = 'C'
            AND donor.slots_needed > 0
            AND collector.slots_needed > 0
            AND donor.account_id != collector.account_id
            AND NOT EXISTS (
                SELECT 1 FROM deal d
                WHERE ((d.account_1_id = donor.account_id AND d.account_2_id = collector.account_id)
                    OR (d.account_1_id = collector.account_id AND d.account_2_id = donor.account_id))
                AND d.status = 'active'
            )
    ) ranked
    WHERE donor_rn = 1 AND collector_rn = 1;

    -- =================================================================
    -- Phase 3: Match Tech Farms with Tech Receivers (unidirectional)
    -- Create temp table inline with matches
    -- =================================================================
    DROP TEMPORARY TABLE IF EXISTS temp_fr_matches;
    CREATE TEMPORARY TABLE temp_fr_matches AS
    SELECT
        LEAST(farm_id, receiver_id) AS account_1_id,
        GREATEST(farm_id, receiver_id) AS account_2_id,
        'unidirectional' AS direction
    FROM (
        SELECT
            farm.account_id AS farm_id,
            receiver.account_id AS receiver_id,
            ROW_NUMBER() OVER (PARTITION BY farm.account_id ORDER BY receiver.priority_score DESC) AS farm_rn,
            ROW_NUMBER() OVER (PARTITION BY receiver.account_id ORDER BY farm.priority_score DESC) AS receiver_rn
        FROM wait_queue farm
        JOIN account farm_acct ON farm.account_id = farm_acct.id
        CROSS JOIN wait_queue receiver
        JOIN account receiver_acct ON receiver.account_id = receiver_acct.id
        WHERE farm_acct.va = 'F'
            AND receiver_acct.va = 'R'
            AND farm.slots_needed > 0
            AND receiver.slots_needed > 0
            AND farm.account_id != receiver.account_id
            AND NOT EXISTS (
                SELECT 1 FROM deal d
                WHERE ((d.account_1_id = farm.account_id AND d.account_2_id = receiver.account_id)
                    OR (d.account_1_id = receiver.account_id AND d.account_2_id = farm.account_id))
                AND d.status = 'active'
            )
    ) ranked
    WHERE farm_rn = 1 AND receiver_rn = 1;

    -- =================================================================
    -- Phase 4: Create all deals from the three match tables
    -- No need for duplicate filtering - ROW_NUMBER partitioning ensures
    -- each account matched at most once per phase, and slots_needed
    -- in wait_queue prevents over-matching across batches
    -- =================================================================
    INSERT INTO deal (account_1_id, account_2_id, direction, created_by)
    SELECT account_1_id, account_2_id, direction, 'system'
    FROM temp_bs_matches
    UNION ALL
    SELECT account_1_id, account_2_id, direction, 'system'
    FROM temp_dc_matches
    UNION ALL
    SELECT account_1_id, account_2_id, direction, 'system'
    FROM temp_fr_matches;

    SET p_deals_created = ROW_COUNT();

    -- Clean up temp tables
    DROP TEMPORARY TABLE IF EXISTS temp_bs_matches;
    DROP TEMPORARY TABLE IF EXISTS temp_dc_matches;
    DROP TEMPORARY TABLE IF EXISTS temp_fr_matches;

    -- =================================================================
    -- Phase 5: Refresh wait_queue to reflect new deals
    -- Simpler approach: just rebuild the queue from account_deal_summary
    -- which automatically reflects the newly created deals
    -- =================================================================
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
        NULL AS next_slot_opens_on,
        DATEDIFF(CURDATE(), COALESCE(a.date_added, CURDATE())) * 100 +
        (ads.slots_available * 10) AS priority_score
    FROM account_deal_summary ads
    JOIN account a ON ads.account_id = a.id
    WHERE ads.slots_available > 0
        AND a.va NOT IN ('H')
        AND NOT EXISTS (
            SELECT 1
            FROM deal_flag df
            WHERE df.flagged_account_id = ads.account_id
                AND df.resolved_on IS NULL
        );
END$$

DELIMITER ;

-- =============================================================================
-- End of V6 Migration
-- =============================================================================
