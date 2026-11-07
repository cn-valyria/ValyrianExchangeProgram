-- =============================================================================
-- V19__locked_slot_deadline.sql
-- VEP 3.0: Don't cancel a deal before a locked aid slot can free up
--
-- A nation whose slots are full of aid outside VEP (sent after the deal
-- started) holds up the deal: it's warned after a day and the deal is
-- cancelled after 5. If any of that outside aid is still pending, the nation
-- can cancel it and free a slot the same day. Once it's all been accepted,
-- CN won't let anyone cancel it, and the slot stays taken until the aid is 10
-- days old, so the 5-day cancellation could come before the nation was ever
-- able to send.
--
-- Now, while the nation's slots are locked (full, and none of its outside
-- aid is pending), deal_state records the CN date its first slot frees. The
-- deal isn't cancelled until the day after that date, so the nation always
-- gets a day to send. The nation is still responsible and still warned
-- after a day, so the flag and its effect on matching stay the same.
--
-- The cancel date is kept for the rest of the stretch the same account is
-- responsible, so it still applies after the slot frees and the deal moves to
-- awaiting_send. It's set when the stretch first locks and isn't pushed back
-- if the nation fills the freed slot with more outside aid.
--
-- Changes:
-- 1. deal_state: slots_locked, slot_frees_on, cancel_not_before
-- 2. Rewritten procedure: update_deal_state (same as V13, plus the columns
--    above)
-- 3. Rewritten view: deal_deadline (cancels_on is no earlier than
--    cancel_not_before)
-- 4. Rebuild deal_state
-- =============================================================================

USE `vep_db`;

ALTER TABLE `deal_state`
    ADD COLUMN `slots_locked` BOOLEAN NOT NULL DEFAULT FALSE
        COMMENT 'Blocked by full slots and none of the blocking nation''s outside aid is pending, so nothing can be cancelled'
        AFTER `blocking_aid_id`,
    ADD COLUMN `slot_frees_on` DATE NULL
        COMMENT 'CN date the blocking nation''s first slot frees (slots_full blocks only)'
        AFTER `slots_locked`,
    ADD COLUMN `cancel_not_before` DATE NULL
        COMMENT 'Earliest CN date the deal can be cancelled for this stretch: a day after a locked slot frees'
        AFTER `responsible_since`;

DELIMITER $$

-- =============================================================================
-- PROCEDURE: update_deal_state
-- Purpose: Recompute deal_state for every active deal (see V9 header for
-- states)
--
-- Same as V13, plus:
--   slots_locked       blocked by full slots, and the blocking nation has no
--                      pending aid that isn't excused (accepted aid can't be
--                      cancelled in CN)
--   slot_frees_on      for full-slot blocks, the first CN date one of the
--                      blocking nation's slots frees (aid date + 10 days)
--   cancel_not_before  slot_frees_on + 1 day, set the first day the stretch
--                      is locked and kept while the same account stays
--                      responsible
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
        SELECT
            aid.id,
            aid.sending_nation_id,
            aid.receiving_nation_id,
            aid.date,
            aid.status,
            EXISTS (SELECT 1 FROM deal_activity da WHERE da.aid_id = aid.id) AS is_deal_leg
        FROM cybernations_db.aid aid
        WHERE aid.status IN (1, 2)
            AND DATE(aid.date) > v_today - INTERVAL 10 DAY
    ),
    nation_aid AS (
        SELECT sending_nation_id AS nation_id, date FROM active_aid
        UNION ALL
        SELECT receiving_nation_id AS nation_id, date FROM active_aid
    ),
    slot_usage AS (
        -- Slots each nation is using, and the first CN date one frees
        SELECT
            nation_id,
            COUNT(*) AS slots_used,
            MIN(DATE(date)) + INTERVAL 10 DAY AS frees_on
        FROM nation_aid
        GROUP BY nation_id
    ),
    deal_aid AS (
        -- Active aid touching either nation of each deal, and whether it's
        -- excused for that deal
        SELECT
            ds.deal_id,
            ds.nation_1_id IN (aa.sending_nation_id, aa.receiving_nation_id) AS touches_1,
            ds.nation_2_id IN (aa.sending_nation_id, aa.receiving_nation_id) AS touches_2,
            aa.status,
            aa.is_deal_leg OR aa.date < CONVERT_TZ(ds.created_on, '+00:00', '-05:00') AS is_excused
        FROM deal_sequence ds
        JOIN active_aid aa ON
            ds.nation_1_id IN (aa.sending_nation_id, aa.receiving_nation_id)
            OR ds.nation_2_id IN (aa.sending_nation_id, aa.receiving_nation_id)
    ),
    excused_usage AS (
        -- Per deal, slots each nation has used for excused aid, and how much
        -- of its other aid is still pending (so it could be cancelled)
        SELECT
            deal_id,
            SUM(is_excused AND touches_1) AS excused_1,
            SUM(is_excused AND touches_2) AS excused_2,
            SUM(NOT is_excused AND status = 1 AND touches_1) AS pending_other_1,
            SUM(NOT is_excused AND status = 1 AND touches_2) AS pending_other_2
        FROM deal_aid
        GROUP BY deal_id
    ),
    pair_aid AS (
        -- Latest active aid between the pair, in either direction
        SELECT
            ds.deal_id,
            aa.id AS aid_id,
            aa.sending_nation_id,
            DATE(aa.date) AS aid_date,
            aa.is_deal_leg OR aa.date < CONVERT_TZ(ds.created_on, '+00:00', '-05:00') AS is_excused,
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
            -- At least one slot holds aid that isn't excused
            COALESCE(IF(ds.next_sender_is_account_1, su1.slots_used, su2.slots_used), 0)
                > COALESCE(IF(ds.next_sender_is_account_1, eu.excused_1, eu.excused_2), 0) AS sender_has_other_aid,
            COALESCE(IF(ds.next_sender_is_account_1, su2.slots_used, su1.slots_used), 0)
                > COALESCE(IF(ds.next_sender_is_account_1, eu.excused_2, eu.excused_1), 0) AS receiver_has_other_aid,
            -- Some of that other aid is still pending, so it can be cancelled
            COALESCE(IF(ds.next_sender_is_account_1, eu.pending_other_1, eu.pending_other_2), 0) > 0 AS sender_has_pending_other,
            COALESCE(IF(ds.next_sender_is_account_1, eu.pending_other_2, eu.pending_other_1), 0) > 0 AS receiver_has_pending_other,
            IF(ds.next_sender_is_account_1, su1.frees_on, su2.frees_on) AS sender_frees_on,
            IF(ds.next_sender_is_account_1, su2.frees_on, su1.frees_on) AS receiver_frees_on,
            pa.aid_id AS pair_aid_id,
            pa.aid_date AS pair_aid_date,
            pa.is_excused AS pair_aid_excused,
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
            prev.responsible_since AS prev_responsible_since,
            prev.cancel_not_before AS prev_cancel_not_before
        FROM deal_sequence ds
        LEFT JOIN slot_usage su1 ON su1.nation_id = ds.nation_1_id
        LEFT JOIN slot_usage su2 ON su2.nation_id = ds.nation_2_id
        LEFT JOIN excused_usage eu ON eu.deal_id = ds.deal_id
        LEFT JOIN pair_aid pa ON pa.deal_id = ds.deal_id AND pa.rn = 1
        LEFT JOIN deal_state prev ON prev.deal_id = ds.deal_id
        WHERE ds.next_sender_is_account_1 IS NOT NULL
    ),
    blocked AS (
        SELECT
            b.*,
            -- First thing stopping the next leg. If it's excused aid, nobody
            -- is at fault
            CASE
                WHEN b.pair_aid_is_leg OR b.prev_activated_on IS NULL THEN NULL
                WHEN b.pair_aid_id IS NOT NULL THEN IF(b.pair_aid_excused, NULL, 'out_of_sequence_aid')
                WHEN b.sender_full THEN IF(b.sender_has_other_aid, 'sender_slots_full', NULL)
                WHEN b.receiver_full THEN IF(b.receiver_has_other_aid, 'receiver_slots_full', NULL)
                ELSE NULL
            END AS new_blocked_reason
        FROM base b
    ),
    classified AS (
        SELECT
            bl.*,
            CASE
                WHEN bl.pair_aid_is_leg THEN 'in_flight'
                WHEN bl.new_blocked_reason IS NOT NULL THEN 'blocked'
                -- Not yet activated, or held up only by excused aid
                WHEN bl.pair_aid_id IS NOT NULL OR bl.sender_full OR bl.receiver_full THEN 'pending_start'
                ELSE 'awaiting_send'
            END AS new_state,
            CASE bl.new_blocked_reason
                WHEN 'sender_slots_full' THEN bl.sender_frees_on
                WHEN 'receiver_slots_full' THEN bl.receiver_frees_on
                ELSE NULL
            END AS new_slot_frees_on,
            CASE bl.new_blocked_reason
                WHEN 'sender_slots_full' THEN NOT bl.sender_has_pending_other
                WHEN 'receiver_slots_full' THEN NOT bl.receiver_has_pending_other
                ELSE FALSE
            END AS new_slots_locked
        FROM blocked bl
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
    ),
    stretch AS (
        SELECT
            r.*,
            r.new_responsible_account_id IS NOT NULL
                AND r.new_responsible_account_id = r.prev_responsible_account_id
                AND r.prev_responsible_since IS NOT NULL AS same_stretch
        FROM responsible r
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
        r.new_slots_locked AS slots_locked,
        r.new_slot_frees_on AS slot_frees_on,
        r.new_responsible_account_id AS responsible_account_id,
        CASE
            WHEN r.new_responsible_account_id IS NULL THEN NULL
            WHEN r.same_stretch THEN r.prev_responsible_since
            ELSE v_today
        END AS responsible_since,
        CASE
            -- Set once per stretch, so refilling a freed slot doesn't push it back
            WHEN r.same_stretch AND r.prev_cancel_not_before IS NOT NULL THEN r.prev_cancel_not_before
            WHEN r.new_slots_locked THEN r.new_slot_frees_on + INTERVAL 1 DAY
            ELSE NULL
        END AS cancel_not_before,
        COALESCE(r.prev_activated_on, IF(r.new_state IN ('in_flight', 'awaiting_send'), v_today, NULL)) AS activated_on
    FROM stretch r;

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
        slots_locked,
        slot_frees_on,
        responsible_account_id,
        responsible_since,
        cancel_not_before,
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
        t.slots_locked,
        t.slot_frees_on,
        t.responsible_account_id,
        t.responsible_since,
        t.cancel_not_before,
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
        slots_locked = t.slots_locked,
        slot_frees_on = t.slot_frees_on,
        responsible_account_id = t.responsible_account_id,
        responsible_since = t.responsible_since,
        cancel_not_before = t.cancel_not_before,
        activated_on = t.activated_on;

    DROP TEMPORARY TABLE IF EXISTS tmp_deal_state;
END$$

DELIMITER ;

-- =============================================================================
-- VIEW: deal_deadline
-- Purpose: Each deal's cancellation clock, with any extension that still
-- applies
--
-- Same as V16, except cancels_on is never before deal_state.cancel_not_before
-- (the day after a locked slot frees). clock_since is unchanged, so the
-- 1-day warning is still raised on the usual day.
-- =============================================================================
CREATE OR REPLACE VIEW `deal_deadline` AS
SELECT
    x.deal_id,
    x.extension_id,
    x.extension_kind,
    x.extended_until,
    x.extended_by,
    x.clock_since,
    CASE
        WHEN x.clock_since IS NULL THEN NULL
        WHEN x.cancel_not_before > x.clock_since + INTERVAL 5 DAY THEN x.cancel_not_before
        ELSE x.clock_since + INTERVAL 5 DAY
    END AS cancels_on
FROM (
    SELECT
        ds.deal_id,
        ds.cancel_not_before,
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

-- =============================================================================
-- Step 4: Rebuild deal_state so locked deals get their cancel date before the
-- next daily check
-- =============================================================================
CALL update_deal_state();

-- =============================================================================
-- End of V19 Migration
-- =============================================================================
