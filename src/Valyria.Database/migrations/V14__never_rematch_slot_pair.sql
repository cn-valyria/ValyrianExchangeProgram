-- =============================================================================
-- V14__never_rematch_slot_pair.sql
-- VEP 3.0: Never match the same pair of slots twice
--
-- deal.unique_slot_pair (V7) allows one deal per slot pair, whatever its
-- status, but match_deals_run (V8) only skipped slots and account pairs with
-- an active deal. Once a deal was cancelled, the same two slots could be each
-- other's best candidate again, and the INSERT failed with a duplicate key.
-- Every later run rebuilt the same candidates and failed the same way, so no
-- deals were matched until the pair was removed by hand.
--
-- A slot pair that has ever had a deal is now never a candidate again. The
-- two accounts can still be matched on a different pair of slots.
--
-- Changes:
-- 1. Rewritten procedure: match_deals_run (only tmp_pairs' filter changed)
-- =============================================================================

USE `vep_db`;

DELIMITER $$

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
        );

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
-- End of V14 Migration
-- =============================================================================
