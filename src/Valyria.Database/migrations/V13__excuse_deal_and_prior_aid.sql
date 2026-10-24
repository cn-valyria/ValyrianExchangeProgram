-- =============================================================================
-- V13__excuse_deal_and_prior_aid.sql
-- VEP 3.0: Don't blame accounts for aid they couldn't have avoided
--
-- update_deal_state (V9) blamed a nation whenever its slots were full, or
-- whenever aid between the pair wasn't the expected leg. That included aid
-- the nation had no choice about:
--   - legs of its other VEP deals (a nation matched into more deals than it
--     has free slots fills them all with deal aid)
--   - aid sent before the deal was created, which was often not yet loaded
--     when the post-matching update_deal_state run activated the deal
-- Accounts that sent nothing but VEP aid were warned and would have been
-- cancelled after 5 days.
--
-- Now a nation is only responsible for a full-slot or out-of-sequence block
-- if the blocking aid is non-deal aid sent on or after the deal's creation.
-- A deal held up only by excused aid goes back to pending_start (waiting,
-- nobody at fault), which also clears responsible_since so the 5-day clock
-- starts over once the nation really is holding it up.
--
-- Changes:
-- 1. Rewritten procedure: update_deal_state
-- 2. Cleanup: resolve open 1-day warnings raised for excused aid, and restart
--    the clock on those deals
-- 3. Rebuild deal_state, then resolve warnings whose account is no longer
--    holding up the deal
-- =============================================================================

USE `vep_db`;

DELIMITER $$

-- =============================================================================
-- PROCEDURE: update_deal_state
-- Purpose: Recompute deal_state for every active deal (see V9 header for
-- states)
--
-- Slot usage counts pending and approved aid sent or received in the last
-- 10 CN days, against 4 + fm + dra slots per account. Aid is excused for a
-- deal if it's a leg of any deal, or was sent before the deal was created.
-- A nation is responsible for full slots only if at least one of them holds
-- aid that isn't excused, and for out-of-sequence aid only if it isn't
-- excused. Otherwise the deal is pending_start: waiting, nobody at fault.
-- Run after track_deal_activity, and again after matching so new deals get
-- a row.
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
            EXISTS (SELECT 1 FROM deal_activity da WHERE da.aid_id = aid.id) AS is_deal_leg
        FROM cybernations_db.aid aid
        WHERE aid.status IN (1, 2)
            AND DATE(aid.date) > v_today - INTERVAL 10 DAY
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
    excused_usage AS (
        -- Per deal, slots each nation has used for excused aid
        SELECT
            ds.deal_id,
            SUM(ds.nation_1_id IN (aa.sending_nation_id, aa.receiving_nation_id)) AS excused_1,
            SUM(ds.nation_2_id IN (aa.sending_nation_id, aa.receiving_nation_id)) AS excused_2
        FROM deal_sequence ds
        JOIN active_aid aa ON
            (ds.nation_1_id IN (aa.sending_nation_id, aa.receiving_nation_id)
                OR ds.nation_2_id IN (aa.sending_nation_id, aa.receiving_nation_id))
            AND (aa.is_deal_leg OR aa.date < CONVERT_TZ(ds.created_on, '+00:00', '-05:00'))
        GROUP BY ds.deal_id
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
            prev.responsible_since AS prev_responsible_since
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
            END AS new_state
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

DELIMITER ;

-- =============================================================================
-- Step 2: Open 1-day warnings for full slots or out-of-sequence aid, where
-- the flagged nation had no non-excused aid to blame when the warning was
-- raised. Slot usage is rebuilt from aid dated in the 10 CN days up to the
-- run that raised the warning (status 4 included, since the loader expires
-- aid after its window). These are resolved, and their deals' clocks are
-- cleared so a nation that has since become responsible starts from today
-- rather than from when it was first wrongly blamed
-- =============================================================================
DROP TEMPORARY TABLE IF EXISTS tmp_excused_flags;

CREATE TEMPORARY TABLE tmp_excused_flags AS
SELECT df.id AS flag_id, df.deal_id, df.flagged_account_id
FROM deal_flag df
JOIN deal d ON d.id = df.deal_id
JOIN account fa ON fa.id = df.flagged_account_id
JOIN account ia ON ia.id = df.innocent_account_id
WHERE df.flag_type = '1_day_inactive'
    AND df.resolved_on IS NULL
    AND d.status = 'active'
    AND df.flag_reason IN ('sender_slots_full', 'receiver_slots_full', 'out_of_sequence_aid')
    AND NOT EXISTS (
        SELECT 1
        FROM cybernations_db.aid x
        WHERE x.status IN (1, 2, 4)
            AND DATE(x.date) > DATE(CONVERT_TZ(df.flagged_on, '+00:00', '-05:00')) - INTERVAL 10 DAY
            AND x.date < CONVERT_TZ(df.flagged_on, '+00:00', '-05:00')
            AND x.date >= CONVERT_TZ(d.created_on, '+00:00', '-05:00')
            AND NOT EXISTS (SELECT 1 FROM deal_activity da WHERE da.aid_id = x.id)
            AND IF(df.flag_reason = 'out_of_sequence_aid',
                x.sending_nation_id = fa.nation_id AND x.receiving_nation_id = ia.nation_id,
                fa.nation_id IN (x.sending_nation_id, x.receiving_nation_id))
    );

UPDATE deal_flag df
JOIN tmp_excused_flags t ON t.flag_id = df.id
SET
    df.resolved_on = CURRENT_TIMESTAMP,
    df.resolved_by = 'system',
    df.admin_notes = 'Resolved by V13: raised for VEP deal aid or aid sent before the deal';

UPDATE deal_state ds
JOIN tmp_excused_flags t ON t.deal_id = ds.deal_id
    AND t.flagged_account_id = ds.responsible_account_id
SET
    ds.responsible_account_id = NULL,
    ds.responsible_since = NULL;

DROP TEMPORARY TABLE IF EXISTS tmp_excused_flags;

-- =============================================================================
-- Step 3: Rebuild deal_state under the new rules, then resolve warnings
-- whose account is no longer holding up the deal (check_deal_inactivity
-- does this for the rest at the next daily run)
-- =============================================================================
CALL update_deal_state();

UPDATE deal_flag df
JOIN deal d ON d.id = df.deal_id
LEFT JOIN deal_state ds ON ds.deal_id = df.deal_id
SET
    df.resolved_on = CURRENT_TIMESTAMP,
    df.resolved_by = 'system',
    df.admin_notes = 'Resolved by V13: account no longer holding up the deal under the corrected rules'
WHERE df.flag_type = '1_day_inactive'
    AND df.resolved_on IS NULL
    AND d.status = 'active'
    AND (ds.responsible_account_id IS NULL OR ds.responsible_account_id <> df.flagged_account_id);

-- =============================================================================
-- End of V13 Migration
-- =============================================================================
