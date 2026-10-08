-- =============================================================================
-- V9__deal_state.sql
-- VEP 3.0: Expected-activity tracking for deals
--
-- A deal is a repeating sequence of "legs", one CN aid offer each. The two
-- slots' cycle values decide the order: the side that sends cash goes first
-- and sends all of its cycles, then the other side sends all of its cycles.
--   Buyer/Seller (1 cash : 1 tech)   B, S, B, S, ...
--   Buyer/Seller (1 cash : 2 tech)   B, S, S, B, S, S, ...
--   Donor/Collector, Farm/Receiver   same sender every leg
--
-- Every aid offer occupies an in-game slot on both nations for 10 days, and
-- two nations can't have two active offers between them. Each daily run puts
-- every active deal in exactly one state:
--   pending_start  Not yet activated: the pair has never been able to
--                  exchange aid (slots full, or old aid still between them).
--                  Nobody is at fault
--   in_flight      A counted leg is active between the pair. Nobody can act
--   awaiting_send  Both nations have a free slot and nothing is active
--                  between them. The expected sender is responsible
--   blocked        Activated, but the exchange can't happen because a nation
--                  used its slots for non-deal aid. That nation is responsible:
--                  - sender_slots_full / receiver_slots_full
--                  - out_of_sequence_aid: aid between the pair that isn't the
--                    expected leg (the wrong side sent). Its sender is
--                    responsible
--
-- Aid counts as a leg only when it comes from the expected sender, to the
-- expected receiver, on or after the deal was created. Amounts and extra
-- assets are ignored. Out-of-sequence aid is never counted.
--
-- Leg outcomes: a leg fails if its offer is cancelled, or expires without
-- being approved. Failures are recorded here and acted on by
-- check_deal_inactivity (V10).
--
-- Changes:
-- 1. deal_activity is rebuilt as one row per counted leg (old rows linked any
--    aid between the pair, including aid sent before the deal existed)
-- 2. New view: deal_sequence (per active deal: whose turn it is)
-- 3. New table: deal_state (per active deal: current state and who is
--    responsible since when)
-- 4. Rewritten procedure: track_deal_activity
-- 5. New procedure: update_deal_state
-- 6. check_deal_inactivity becomes a no-op until V10, and unresolved flags
--    from the old daily-activity rules are resolved
--
-- Dates: CN dates are server UTC shifted to -05:00, as in the aid_activity view
-- =============================================================================

USE `vep_db`;

-- =============================================================================
-- Step 1: Rebuild deal_activity as a leg log
-- Old rows were linked under the old rules; track_deal_activity relinks them
-- at the end of this migration
-- =============================================================================
DELETE FROM `deal_activity`;

ALTER TABLE `deal_activity`
    ADD COLUMN `leg_number` INT NOT NULL COMMENT '1-based, in the order legs were linked' AFTER `aid_id`,
    ADD COLUMN `linked_late` BOOLEAN NOT NULL DEFAULT FALSE
        COMMENT 'Aid was already past its 10-day window when linked, so its outcome is unknown and it counts as completed',
    ADD COLUMN `approved_on` DATE NULL COMMENT 'CN date the offer was first seen approved',
    ADD COLUMN `failed_on` DATE NULL COMMENT 'CN date the offer was first seen cancelled or expired unapproved',
    ADD COLUMN `failure_reason` ENUM('cancelled', 'expired') NULL,
    ADD UNIQUE KEY `unique_aid` (`aid_id`),
    ADD UNIQUE KEY `unique_deal_leg` (`deal_id`, `leg_number`);

-- =============================================================================
-- VIEW: deal_sequence
-- Purpose: For each active deal, who sends the next leg
--
-- legs_counted excludes failed legs, so a failed offer doesn't move the turn
-- on. Deals whose slots send nothing (round_length 0) have no next sender.
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
        (4 + IF(a1.fm = 1, 1, 0) + IF(a1.dra = 1, 1, 0)) AS slots_total_1,
        (4 + IF(a2.fm = 1, 1, 0) + IF(a2.dra = 1, 1, 0)) AS slots_total_2,
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

-- =============================================================================
-- TABLE: deal_state
-- Purpose: Current expected-activity state of each active deal
--
-- Rebuilt by update_deal_state on every loader run. The *_since dates carry
-- over between runs while the value is unchanged, so DATEDIFF(today,
-- responsible_since) is how many days the responsible account has held up
-- the deal. check_deal_inactivity escalates on that.
-- =============================================================================
CREATE TABLE IF NOT EXISTS `deal_state` (
    `deal_id` INT PRIMARY KEY,
    `state` ENUM('pending_start', 'in_flight', 'awaiting_send', 'blocked') NOT NULL,
    `state_since` DATE NOT NULL COMMENT 'CN date the deal entered this state',
    `blocked_reason` ENUM('sender_slots_full', 'receiver_slots_full', 'out_of_sequence_aid') NULL,
    `legs_counted` INT NOT NULL DEFAULT 0 COMMENT 'Linked legs that have not failed, including any in flight',
    `next_sender_account_id` INT NOT NULL,
    `next_receiver_account_id` INT NOT NULL,
    `in_flight_aid_id` INT NULL COMMENT 'Counted leg currently active between the pair',
    `in_flight_until` DATE NULL COMMENT 'First CN date the in-flight leg no longer occupies a slot',
    `blocking_aid_id` INT NULL COMMENT 'Out-of-sequence aid active between the pair',
    `responsible_account_id` INT NULL COMMENT 'Account holding up the deal (awaiting_send or blocked)',
    `responsible_since` DATE NULL COMMENT 'CN date responsible_account_id became responsible',
    `activated_on` DATE NULL COMMENT 'CN date the pair could first exchange aid',
    `updated_on` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

    FOREIGN KEY (`deal_id`) REFERENCES `deal`(`id`) ON DELETE CASCADE,
    FOREIGN KEY (`next_sender_account_id`) REFERENCES `account`(`id`) ON DELETE CASCADE,
    FOREIGN KEY (`next_receiver_account_id`) REFERENCES `account`(`id`) ON DELETE CASCADE,
    FOREIGN KEY (`responsible_account_id`) REFERENCES `account`(`id`) ON DELETE CASCADE,

    INDEX `idx_state` (`state`),
    INDEX `idx_responsible` (`responsible_account_id`, `responsible_since`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Expected-activity state per active deal';

DELIMITER $$

-- =============================================================================
-- PROCEDURE: track_deal_activity
-- Purpose: Link aid to deals as legs, and record each leg's outcome
--
-- Flow:
-- 1. Per active deal, link the earliest unlinked aid from the expected sender
--    to the expected receiver dated on or after both the deal's creation and
--    its latest leg. Repeats until nothing links, since each link moves the
--    turn on (backfills can link several legs per deal)
-- 2. Record approvals and failures. The loader rewrites aid older than 10
--    days to status 4 and missing aid to status 3, so outcomes are captured
--    the first time they're seen instead of read from aid.status later
-- =============================================================================
DROP PROCEDURE IF EXISTS `track_deal_activity`$$

CREATE PROCEDURE `track_deal_activity`()
MODIFIES SQL DATA
BEGIN
    DECLARE v_today DATE DEFAULT DATE(CONVERT_TZ(UTC_TIMESTAMP(), '+00:00', '-05:00'));
    DECLARE v_linked INT DEFAULT 1;
    DECLARE v_iterations INT DEFAULT 0;

    -- Step 1: Link legs, at most one per deal per pass
    WHILE v_linked > 0 AND v_iterations < 100 DO
        SET v_iterations = v_iterations + 1;

        INSERT INTO deal_activity (
            deal_id,
            aid_id,
            leg_number,
            expected_sender_nation_id,
            expected_receiver_nation_id,
            aid_sent,
            aid_received,
            activity_date,
            linked_late
        )
        SELECT
            c.deal_id,
            c.aid_id,
            c.leg_number,
            c.sender_nation_id,
            c.receiver_nation_id,
            TRUE,
            TRUE,
            c.activity_date,
            c.activity_date <= v_today - INTERVAL 10 DAY
        FROM (
            SELECT
                ds.deal_id,
                aid.id AS aid_id,
                ds.legs_linked + 1 AS leg_number,
                aid.sending_nation_id AS sender_nation_id,
                aid.receiving_nation_id AS receiver_nation_id,
                DATE(aid.date) AS activity_date,
                ROW_NUMBER() OVER (PARTITION BY ds.deal_id ORDER BY aid.date, aid.id) AS rn
            FROM deal_sequence ds
            JOIN cybernations_db.aid aid ON
                aid.sending_nation_id = IF(ds.next_sender_is_account_1, ds.nation_1_id, ds.nation_2_id)
                AND aid.receiving_nation_id = IF(ds.next_sender_is_account_1, ds.nation_2_id, ds.nation_1_id)
            WHERE ds.next_sender_is_account_1 IS NOT NULL
                AND DATE(aid.date) >= DATE(CONVERT_TZ(ds.created_on, '+00:00', '-05:00'))
                AND DATE(aid.date) >= COALESCE(ds.last_leg_date, '1000-01-01')
                AND NOT EXISTS (
                    SELECT 1 FROM deal_activity da WHERE da.aid_id = aid.id
                )
        ) c
        WHERE c.rn = 1;

        SET v_linked = ROW_COUNT();
    END WHILE;

    -- Step 2a: Approvals
    UPDATE deal_activity da
    JOIN cybernations_db.aid aid ON da.aid_id = aid.id
    SET da.approved_on = v_today
    WHERE da.approved_on IS NULL
        AND aid.status = 2;

    -- Step 2b: Failures. A cancelled offer always fails. An offer that is
    -- expired, or has reached the end of its window, fails only if it was never
    -- seen approved and was linked while still inside its window (otherwise
    -- there's no way to tell). Checking the window directly catches it the day
    -- it lapses, before the loader's status 4 sweep
    UPDATE deal_activity da
    JOIN cybernations_db.aid aid ON da.aid_id = aid.id
    SET
        da.failed_on = v_today,
        da.failure_reason = IF(aid.status = 3, 'cancelled', 'expired')
    WHERE da.failed_on IS NULL
        AND (
            aid.status = 3
            OR (
                da.approved_on IS NULL
                AND NOT da.linked_late
                AND (aid.status = 4 OR DATE(aid.date) <= v_today - INTERVAL 10 DAY)
            )
        );
END$$

-- =============================================================================
-- PROCEDURE: update_deal_state
-- Purpose: Recompute deal_state for every active deal (see header for states)
--
-- Slot usage counts pending and approved aid sent or received in the last
-- 10 CN days, against 4 + fm + dra slots per account. Run after
-- track_deal_activity, and again after matching so new deals get a row.
-- =============================================================================
DROP PROCEDURE IF EXISTS `update_deal_state`$$

CREATE PROCEDURE `update_deal_state`()
MODIFIES SQL DATA
BEGIN
    DECLARE v_today DATE DEFAULT DATE(CONVERT_TZ(UTC_TIMESTAMP(), '+00:00', '-05:00'));

    DELETE ds
    FROM deal_state ds
    JOIN deal d ON ds.deal_id = d.id
    WHERE d.status <> 'active';

    DROP TEMPORARY TABLE IF EXISTS tmp_deal_state;

    CREATE TEMPORARY TABLE tmp_deal_state AS
    WITH active_aid AS (
        SELECT id, sending_nation_id, receiving_nation_id, date
        FROM cybernations_db.aid
        WHERE status IN (1, 2)
            AND DATE(date) > v_today - INTERVAL 10 DAY
    ),
    slot_usage AS (
        SELECT nation_id, COUNT(*) AS slots_used
        FROM (
            SELECT sending_nation_id AS nation_id FROM active_aid
            UNION ALL
            SELECT receiving_nation_id AS nation_id FROM active_aid
        ) x
        GROUP BY nation_id
    ),
    pair_aid AS (
        -- Latest active aid between the pair, in either direction
        SELECT
            ds.deal_id,
            aa.id AS aid_id,
            aa.sending_nation_id,
            DATE(aa.date) AS aid_date,
            ROW_NUMBER() OVER (PARTITION BY ds.deal_id ORDER BY aa.date DESC, aa.id DESC) AS rn
        FROM deal_sequence ds
        JOIN active_aid aa ON
            (aa.sending_nation_id = ds.nation_1_id AND aa.receiving_nation_id = ds.nation_2_id)
            OR (aa.sending_nation_id = ds.nation_2_id AND aa.receiving_nation_id = ds.nation_1_id)
    ),
    base AS (
        SELECT
            ds.deal_id,
            ds.legs_counted,
            ds.legs_linked,
            ds.first_leg_date,
            IF(ds.next_sender_is_account_1, ds.account_1_id, ds.account_2_id) AS sender_account_id,
            IF(ds.next_sender_is_account_1, ds.account_2_id, ds.account_1_id) AS receiver_account_id,
            COALESCE(IF(ds.next_sender_is_account_1, su1.slots_used, su2.slots_used), 0)
                >= IF(ds.next_sender_is_account_1, ds.slots_total_1, ds.slots_total_2) AS sender_full,
            COALESCE(IF(ds.next_sender_is_account_1, su2.slots_used, su1.slots_used), 0)
                >= IF(ds.next_sender_is_account_1, ds.slots_total_2, ds.slots_total_1) AS receiver_full,
            pa.aid_id AS pair_aid_id,
            pa.aid_date AS pair_aid_date,
            IF(pa.sending_nation_id = ds.nation_1_id, ds.account_1_id, ds.account_2_id) AS pair_aid_sender_account_id,
            EXISTS (
                SELECT 1 FROM deal_activity da
                WHERE da.aid_id = pa.aid_id AND da.deal_id = ds.deal_id
            ) AS pair_aid_is_leg,
            -- Any linked leg means the pair has exchanged aid, so the deal is active
            COALESCE(prev.activated_on, IF(ds.legs_linked > 0, ds.first_leg_date, NULL)) AS prev_activated_on,
            prev.state AS prev_state,
            prev.state_since AS prev_state_since,
            prev.responsible_account_id AS prev_responsible_account_id,
            prev.responsible_since AS prev_responsible_since
        FROM deal_sequence ds
        LEFT JOIN slot_usage su1 ON su1.nation_id = ds.nation_1_id
        LEFT JOIN slot_usage su2 ON su2.nation_id = ds.nation_2_id
        LEFT JOIN pair_aid pa ON pa.deal_id = ds.deal_id AND pa.rn = 1
        LEFT JOIN deal_state prev ON prev.deal_id = ds.deal_id
        WHERE ds.next_sender_is_account_1 IS NOT NULL
    ),
    classified AS (
        SELECT
            b.*,
            CASE
                WHEN b.pair_aid_is_leg THEN 'in_flight'
                -- Before activation nothing is anyone's fault
                WHEN b.prev_activated_on IS NULL
                    AND (b.pair_aid_id IS NOT NULL OR b.sender_full OR b.receiver_full) THEN 'pending_start'
                WHEN b.pair_aid_id IS NOT NULL OR b.sender_full OR b.receiver_full THEN 'blocked'
                ELSE 'awaiting_send'
            END AS new_state,
            CASE
                WHEN b.pair_aid_is_leg OR b.prev_activated_on IS NULL THEN NULL
                WHEN b.pair_aid_id IS NOT NULL THEN 'out_of_sequence_aid'
                WHEN b.sender_full THEN 'sender_slots_full'
                WHEN b.receiver_full THEN 'receiver_slots_full'
                ELSE NULL
            END AS new_blocked_reason
        FROM base b
    ),
    responsible AS (
        SELECT
            c.*,
            CASE
                WHEN c.new_state = 'awaiting_send' THEN c.sender_account_id
                WHEN c.new_blocked_reason = 'out_of_sequence_aid' THEN c.pair_aid_sender_account_id
                WHEN c.new_blocked_reason = 'sender_slots_full' THEN c.sender_account_id
                WHEN c.new_blocked_reason = 'receiver_slots_full' THEN c.receiver_account_id
                ELSE NULL
            END AS new_responsible_account_id
        FROM classified c
    )
    SELECT
        r.deal_id,
        r.new_state AS state,
        IF(r.prev_state = r.new_state, r.prev_state_since, v_today) AS state_since,
        r.new_blocked_reason AS blocked_reason,
        r.legs_counted,
        r.sender_account_id AS next_sender_account_id,
        r.receiver_account_id AS next_receiver_account_id,
        IF(r.new_state = 'in_flight', r.pair_aid_id, NULL) AS in_flight_aid_id,
        IF(r.new_state = 'in_flight', r.pair_aid_date + INTERVAL 10 DAY, NULL) AS in_flight_until,
        IF(r.new_blocked_reason = 'out_of_sequence_aid', r.pair_aid_id, NULL) AS blocking_aid_id,
        r.new_responsible_account_id AS responsible_account_id,
        CASE
            WHEN r.new_responsible_account_id IS NULL THEN NULL
            WHEN r.new_responsible_account_id = r.prev_responsible_account_id
                AND r.prev_responsible_since IS NOT NULL THEN r.prev_responsible_since
            ELSE v_today
        END AS responsible_since,
        COALESCE(r.prev_activated_on, IF(r.new_state IN ('in_flight', 'awaiting_send'), v_today, NULL)) AS activated_on
    FROM responsible r;

    INSERT INTO deal_state (
        deal_id,
        state,
        state_since,
        blocked_reason,
        legs_counted,
        next_sender_account_id,
        next_receiver_account_id,
        in_flight_aid_id,
        in_flight_until,
        blocking_aid_id,
        responsible_account_id,
        responsible_since,
        activated_on
    )
    SELECT
        t.deal_id,
        t.state,
        t.state_since,
        t.blocked_reason,
        t.legs_counted,
        t.next_sender_account_id,
        t.next_receiver_account_id,
        t.in_flight_aid_id,
        t.in_flight_until,
        t.blocking_aid_id,
        t.responsible_account_id,
        t.responsible_since,
        t.activated_on
    FROM tmp_deal_state t
    ON DUPLICATE KEY UPDATE
        state = t.state,
        state_since = t.state_since,
        blocked_reason = t.blocked_reason,
        legs_counted = t.legs_counted,
        next_sender_account_id = t.next_sender_account_id,
        next_receiver_account_id = t.next_receiver_account_id,
        in_flight_aid_id = t.in_flight_aid_id,
        in_flight_until = t.in_flight_until,
        blocking_aid_id = t.blocking_aid_id,
        responsible_account_id = t.responsible_account_id,
        responsible_since = t.responsible_since,
        activated_on = t.activated_on;

    DROP TEMPORARY TABLE IF EXISTS tmp_deal_state;
END$$

-- =============================================================================
-- PROCEDURE: check_deal_inactivity
-- Purpose: No-op until V10 rewrites it on top of deal_state. The old version
-- expected aid every day and cancelled deals after 5 days without any
-- =============================================================================
DROP PROCEDURE IF EXISTS `check_deal_inactivity`$$

CREATE PROCEDURE `check_deal_inactivity`()
MODIFIES SQL DATA
BEGIN
    DO 0;
END$$

DELIMITER ;

-- =============================================================================
-- Step 2: Resolve open flags raised under the old rules. They always blamed
-- the deal's first slot and never resolved, so they wrongly keep accounts out
-- of matching
-- =============================================================================
UPDATE `deal_flag`
SET
    resolved_on = CURRENT_TIMESTAMP,
    resolved_by = 'system',
    admin_notes = 'Resolved by V9: raised under the old daily-activity rules'
WHERE resolved_on IS NULL;

-- =============================================================================
-- Step 3: Relink activity for existing deals and build their state
-- =============================================================================
CALL track_deal_activity();
CALL update_deal_state();

-- =============================================================================
-- End of V9 Migration
-- =============================================================================
