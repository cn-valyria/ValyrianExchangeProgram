-- =============================================================================
-- Simple Test Script: Run Deal Matching Loop
-- Purpose: Replicate the C# loop for testing match_deals_batch
--
-- Usage: Just run this script in MySQL Workbench
-- =============================================================================

USE `vep_db`;

DELIMITER $$

DROP PROCEDURE IF EXISTS `run_matching_loop`$$

CREATE PROCEDURE `run_matching_loop`()
BEGIN
    DECLARE v_iteration INT DEFAULT 0;
    DECLARE v_deals_created INT DEFAULT 1;
    DECLARE v_total_deals INT DEFAULT 0;
    DECLARE v_batch_deals INT;

    -- Initialize the wait queue
    CALL populate_wait_queue();

    -- Get initial deal count
    SELECT COUNT(*) INTO v_total_deals FROM deal WHERE status = 'active';

    SELECT 'Starting deal matching loop...' AS status;

    -- Loop until no more deals created or max iterations
    WHILE v_deals_created > 0 AND v_iteration < 500 DO
        SET v_iteration = v_iteration + 1;

        -- Call the batch matching procedure (uses OUT parameter)
        CALL match_deals_batch(v_deals_created);

        -- Update total
        SET v_total_deals = v_total_deals + v_deals_created;

        -- Print progress every 10 iterations
        IF v_iteration % 10 = 0 THEN
            SELECT CONCAT('Iteration ', v_iteration, ': Created ', v_deals_created, ' deals (total: ', v_total_deals, ')') AS progress;
        END IF;
    END WHILE;

    -- Display final results
    SELECT
        v_iteration AS total_iterations,
        v_total_deals AS total_deals_created,
        CASE
            WHEN v_iteration >= 500 THEN 'WARNING: Hit max iterations limit!'
            ELSE 'Completed successfully'
        END AS status;

    -- Show wait queue stats
    SELECT
        COUNT(*) AS accounts_still_waiting,
        SUM(slots_needed) AS total_slots_needed
    FROM wait_queue;
END$$

DELIMITER ;

-- Run the loop
CALL run_matching_loop();

-- Clean up
DROP PROCEDURE IF EXISTS `run_matching_loop`;
