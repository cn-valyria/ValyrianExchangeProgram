-- =============================================================================
-- V4__stored_procedures.sql
-- Stored procedures for data processing and business logic
--
-- This file contains procedures for:
-- 1. Cybernations data updates (nation, alliance, aid, war)
-- 2. VEP transaction processing
-- 3. List management and recipient calculations
-- =============================================================================

USE `cybernations_db`;

DELIMITER $$

-- =============================================================================
-- PROCEDURE: update_nation_data
-- Purpose: Process daily nation file import
--
-- Flow:
-- 1. Insert new nations from staging table (todays_file_nation)
-- 2. Update changed nations
-- 3. Delete nations not in today's file (nation deleted from game)
-- =============================================================================
CREATE PROCEDURE `update_nation_data`()
MODIFIES SQL DATA
BEGIN
    -- Insert new nations
    INSERT INTO nation (
        id, nation_name, ruler_name, alliance_id, alliance_date,
        alliance_status, government_type, religion, team, created,
        technology, infrastructure, base_land, war_status, votes,
        strength, defcon, base_soldiers, tanks, cruise_missiles,
        nukes, recent_activity, resource_1, resource_2,
        connected_resource_1, connected_resource_2, connected_resource_3,
        connected_resource_4, connected_resource_5, connected_resource_6,
        connected_resource_7, connected_resource_8, connected_resource_9,
        connected_resource_10, audit_updated_on, audit_updated_by)
    SELECT
        nw.nation_id, nw.nation_name, nw.ruler_name, nw.alliance_id, nw.alliance_date,
        nw.alliance_status, nw.government_type, nw.religion, nw.team, nw.created,
        nw.technology, nw.infrastructure, nw.base_land, nw.war_status, nw.votes,
        nw.strength, nw.defcon, nw.base_soldiers, nw.tanks, nw.cruise_missiles,
        nw.nukes, nw.recent_activity, nw.resource_1, nw.resource_2,
        nw.connected_resource_1, nw.connected_resource_2, nw.connected_resource_3,
        nw.connected_resource_4, nw.connected_resource_5, nw.connected_resource_6,
        nw.connected_resource_7, nw.connected_resource_8, nw.connected_resource_9,
        nw.connected_resource_10, CURRENT_TIMESTAMP, file_name
    FROM todays_file_nation nw
    LEFT JOIN nation cur ON nw.nation_id = cur.id
    WHERE cur.id IS NULL;

    -- Update changed nations (only those with actual changes)
    UPDATE nation cur
    INNER JOIN todays_file_nation nw
        ON nw.nation_id = cur.id AND (
            nw.nation_name <> cur.nation_name OR
            nw.ruler_name <> cur.ruler_name OR
            nw.alliance_id <> cur.alliance_id OR
            nw.alliance_date <> cur.alliance_date OR
            nw.alliance_status <> cur.alliance_status OR
            nw.government_type <> cur.government_type OR
            nw.religion <> cur.religion OR
            nw.team <> cur.team OR
            nw.created <> cur.created OR
            nw.technology <> cur.technology OR
            nw.infrastructure <> cur.infrastructure OR
            nw.base_land <> cur.base_land OR
            nw.war_status <> cur.war_status OR
            nw.votes <> cur.votes OR
            nw.strength <> cur.strength OR
            nw.defcon <> cur.defcon OR
            nw.base_soldiers <> cur.base_soldiers OR
            nw.tanks <> cur.tanks OR
            nw.cruise_missiles <> cur.cruise_missiles OR
            nw.nukes <> cur.nukes OR
            nw.recent_activity <> cur.recent_activity OR
            nw.resource_1 <> cur.resource_1 OR
            nw.resource_2 <> cur.resource_2 OR
            nw.connected_resource_1 <> cur.connected_resource_1 OR
            nw.connected_resource_2 <> cur.connected_resource_2 OR
            nw.connected_resource_3 <> cur.connected_resource_3 OR
            nw.connected_resource_4 <> cur.connected_resource_4 OR
            nw.connected_resource_5 <> cur.connected_resource_5 OR
            nw.connected_resource_6 <> cur.connected_resource_6 OR
            nw.connected_resource_7 <> cur.connected_resource_7 OR
            nw.connected_resource_8 <> cur.connected_resource_8 OR
            nw.connected_resource_9 <> cur.connected_resource_9 OR
            nw.connected_resource_10 <> cur.connected_resource_10)
    SET
        cur.nation_name = nw.nation_name,
        cur.ruler_name = nw.ruler_name,
        cur.alliance_id = nw.alliance_id,
        cur.alliance_date = nw.alliance_date,
        cur.alliance_status = nw.alliance_status,
        cur.government_type = nw.government_type,
        cur.religion = nw.religion,
        cur.team = nw.team,
        cur.created = nw.created,
        cur.technology = nw.technology,
        cur.infrastructure = nw.infrastructure,
        cur.base_land = nw.base_land,
        cur.war_status = nw.war_status,
        cur.votes = nw.votes,
        cur.strength = nw.strength,
        cur.defcon = nw.defcon,
        cur.base_soldiers = nw.base_soldiers,
        cur.tanks = nw.tanks,
        cur.cruise_missiles = nw.cruise_missiles,
        cur.nukes = nw.nukes,
        cur.recent_activity = nw.recent_activity,
        cur.resource_1 = nw.resource_1,
        cur.resource_2 = nw.resource_2,
        cur.connected_resource_1 = nw.connected_resource_1,
        cur.connected_resource_2 = nw.connected_resource_2,
        cur.connected_resource_3 = nw.connected_resource_3,
        cur.connected_resource_4 = nw.connected_resource_4,
        cur.connected_resource_5 = nw.connected_resource_5,
        cur.connected_resource_6 = nw.connected_resource_6,
        cur.connected_resource_7 = nw.connected_resource_7,
        cur.connected_resource_8 = nw.connected_resource_8,
        cur.connected_resource_9 = nw.connected_resource_9,
        cur.connected_resource_10 = nw.connected_resource_10,
        cur.audit_updated_on = CURRENT_TIMESTAMP,
        cur.audit_updated_by = nw.file_name;

    -- Delete nations not in today's file
    DELETE cur
    FROM nation cur
    LEFT JOIN todays_file_nation nw ON cur.id = nw.nation_id
    WHERE nw.nation_id IS NULL;
END$$

-- =============================================================================
-- PROCEDURE: update_alliance_data
-- Purpose: Process daily alliance file import
--
-- Flow: Same pattern as update_nation_data
-- =============================================================================
CREATE PROCEDURE `update_alliance_data`()
MODIFIES SQL DATA
BEGIN
    -- Insert new alliances
    INSERT INTO alliance (
        id, name, updated, total_nations, active_nations, percent_active,
        strength, average_strength, score, land, infrastructure, technology,
        war, peace, soldiers, tanks, cruise, nukes, aircraft, navy, anarchy,
        audit_updated_on, audit_updated_by
    )
    SELECT
        nw.alliance_id, nw.name, nw.updated, nw.total_nations, nw.active_nations,
        nw.percent_active, nw.strength, nw.average_strength, nw.score, nw.land,
        nw.infrastructure, nw.technology, nw.war, nw.peace, nw.soldiers, nw.tanks,
        nw.cruise, nw.nukes, nw.aircraft, nw.navy, nw.anarchy,
        CURRENT_TIMESTAMP, file_name
    FROM todays_file_alliance nw
    LEFT JOIN alliance cur ON nw.alliance_id = cur.id
    WHERE cur.id IS NULL;

    -- Update changed alliances
    UPDATE alliance cur
    INNER JOIN todays_file_alliance nw
        ON nw.alliance_id = cur.id AND (
            nw.name <> cur.name OR
            nw.updated <> cur.updated OR
            nw.total_nations <> cur.total_nations OR
            nw.active_nations <> cur.active_nations OR
            nw.percent_active <> cur.percent_active OR
            nw.strength <> cur.strength OR
            nw.average_strength <> cur.average_strength OR
            nw.score <> cur.score OR
            nw.land <> cur.land OR
            nw.infrastructure <> cur.infrastructure OR
            nw.technology <> cur.technology OR
            nw.war <> cur.war OR
            nw.peace <> cur.peace OR
            nw.soldiers <> cur.soldiers OR
            nw.tanks <> cur.tanks OR
            nw.cruise <> cur.cruise OR
            nw.nukes <> cur.nukes OR
            nw.aircraft <> cur.aircraft OR
            nw.navy <> cur.navy OR
            nw.anarchy <> cur.anarchy)
    SET
        cur.name = nw.name,
        cur.updated = nw.updated,
        cur.total_nations = nw.total_nations,
        cur.active_nations = nw.active_nations,
        cur.percent_active = nw.percent_active,
        cur.strength = nw.strength,
        cur.average_strength = nw.average_strength,
        cur.score = nw.score,
        cur.land = nw.land,
        cur.infrastructure = nw.infrastructure,
        cur.technology = nw.technology,
        cur.war = nw.war,
        cur.peace = nw.peace,
        cur.soldiers = nw.soldiers,
        cur.tanks = nw.tanks,
        cur.cruise = nw.cruise,
        cur.nukes = nw.nukes,
        cur.aircraft = nw.aircraft,
        cur.navy = nw.navy,
        cur.anarchy = nw.anarchy,
        cur.audit_updated_on = CURRENT_TIMESTAMP,
        cur.audit_updated_by = nw.file_name;

    -- Delete alliances not in today's file
    DELETE cur
    FROM alliance cur
    LEFT JOIN todays_file_alliance nw ON cur.id = nw.alliance_id
    WHERE nw.alliance_id IS NULL;
END$$

-- =============================================================================
-- PROCEDURE: update_aid_data
-- Purpose: Process daily aid file import
--
-- Special logic: Marks missing active aid as deleted (status=3)
-- Only cares about aid from the last 10 days (older aid is expired anyway)
-- =============================================================================
CREATE PROCEDURE `update_aid_data`()
MODIFIES SQL DATA
BEGIN
    -- Insert new aid
    INSERT INTO aid (
        id, sending_nation_id, sending_alliance_id, sending_team,
        receiving_nation_id, receiving_alliance_id, receiving_team,
        status, money, technology, soldiers, date, reason,
        audit_updated_on, audit_updated_by)
    SELECT
        nw.aid_id, nw.sending_nation_id, nw.sending_alliance_id, nw.sending_team,
        nw.receiving_nation_id, nw.receiving_alliance_id, nw.receiving_team,
        nw.status, nw.money, nw.technology, nw.soldiers, nw.date, nw.reason,
        CURRENT_TIMESTAMP, file_name
    FROM todays_file_aid nw
    LEFT JOIN aid cur ON nw.aid_id = cur.id
    WHERE cur.id IS NULL;

    -- Update changed aid
    UPDATE aid cur
    INNER JOIN todays_file_aid nw
        ON nw.aid_id = cur.id AND (
            nw.sending_nation_id <> cur.sending_nation_id OR
            nw.sending_alliance_id <> cur.sending_alliance_id OR
            nw.sending_team <> cur.sending_team OR
            nw.receiving_nation_id <> cur.receiving_nation_id OR
            nw.receiving_alliance_id <> cur.receiving_alliance_id OR
            nw.receiving_team <> cur.receiving_team OR
            nw.status <> cur.status OR
            nw.money <> cur.money OR
            nw.technology <> cur.technology OR
            nw.soldiers <> cur.soldiers OR
            nw.date <> cur.date OR
            nw.reason <> cur.reason)
    SET
        cur.sending_nation_id = nw.sending_nation_id,
        cur.sending_alliance_id = nw.sending_alliance_id,
        cur.sending_team = nw.sending_team,
        cur.receiving_nation_id = nw.receiving_nation_id,
        cur.receiving_alliance_id = nw.receiving_alliance_id,
        cur.receiving_team = nw.receiving_team,
        cur.status = nw.status,
        cur.money = nw.money,
        cur.technology = nw.technology,
        cur.soldiers = nw.soldiers,
        cur.date = nw.date,
        cur.reason = nw.reason,
        cur.audit_updated_on = CURRENT_TIMESTAMP,
        cur.audit_updated_by = nw.file_name;

    -- Mark missing active aid as deleted
    -- Only cares about recent aid (last 10 days) that's still active
    UPDATE aid
    SET status = 3
    WHERE id NOT IN (SELECT aid_id FROM todays_file_aid)
        AND date > CURDATE() - INTERVAL 9 DAY
        AND status <> 3;
END$$

-- =============================================================================
-- PROCEDURE: update_war_data
-- Purpose: Process daily war file import
--
-- Note: Does NOT delete wars not in today's file - we want historical data
-- =============================================================================
CREATE PROCEDURE `update_war_data`()
MODIFIES SQL DATA
BEGIN
    -- Insert new wars
    INSERT INTO war (
        id, attacking_nation_id, attacking_alliance_id, attacking_team,
        defending_nation_id, defending_alliance_id, defending_team,
        war_status, begin_date, end_date, reason, destruction,
        attack_percent, defend_percent, audit_updated_on, audit_updated_by)
    SELECT
        nw.war_id, nw.attacking_nation_id, nw.attacking_alliance_id, nw.attacking_team,
        nw.defending_nation_id, nw.defending_alliance_id, nw.defending_team,
        nw.war_status, nw.begin_date, nw.end_date, nw.reason, nw.destruction,
        nw.attack_percent, nw.defend_percent, CURRENT_TIMESTAMP, file_name
    FROM todays_file_war nw
    LEFT JOIN war cur ON nw.war_id = cur.id
    WHERE cur.id IS NULL;

    -- Update changed wars
    UPDATE war cur
    INNER JOIN todays_file_war nw
        ON nw.war_id = cur.id AND (
            nw.attacking_nation_id <> cur.attacking_nation_id OR
            nw.attacking_alliance_id <> cur.attacking_alliance_id OR
            nw.attacking_team <> cur.attacking_team OR
            nw.defending_nation_id <> cur.defending_nation_id OR
            nw.defending_alliance_id <> cur.defending_alliance_id OR
            nw.defending_team <> cur.defending_team OR
            nw.war_status <> cur.war_status OR
            nw.begin_date <> cur.begin_date OR
            nw.end_date <> cur.end_date OR
            nw.reason <> cur.reason OR
            nw.destruction <> cur.destruction OR
            nw.attack_percent <> cur.attack_percent OR
            nw.defend_percent <> cur.defend_percent)
    SET
        cur.attacking_nation_id = nw.attacking_nation_id,
        cur.attacking_alliance_id = nw.attacking_alliance_id,
        cur.attacking_team = nw.attacking_team,
        cur.defending_nation_id = nw.defending_nation_id,
        cur.defending_alliance_id = nw.defending_alliance_id,
        cur.defending_team = nw.defending_team,
        cur.war_status = nw.war_status,
        cur.begin_date = nw.begin_date,
        cur.end_date = nw.end_date,
        cur.reason = nw.reason,
        cur.destruction = nw.destruction,
        cur.attack_percent = nw.attack_percent,
        cur.defend_percent = nw.defend_percent,
        cur.audit_updated_on = CURRENT_TIMESTAMP,
        cur.audit_updated_by = nw.file_name;

    -- NOTE: We do NOT delete wars - we want to keep historical war data
END$$

DELIMITER ;

-- =============================================================================

USE `vep_db`;

DELIMITER $$

-- =============================================================================
-- PROCEDURE: load_todays_transactions
-- Purpose: Convert new aid records into VEP transactions
--
-- Logic:
-- - Finds aid sent/received by VEP accounts
-- - Creates transaction records with appropriate rates and values
-- - Only processes aid created after account was added (date_added check)
-- - Excludes 'H' (hold) VA type accounts
-- =============================================================================
CREATE PROCEDURE `load_todays_transactions`()
MODIFIES SQL DATA
BEGIN
    -- Process sent aid
    INSERT INTO transaction (
        aid_id, sending_nation_id, receiving_nation_id, reason_override,
        lu, co, rate, ct, cc, tc, tt)
    SELECT
        sent_aid.id AS aid_id,
        account.nation_id AS sending_nation_id,
        received_account.nation_id AS received_nation_id,
        NULL AS reason_override,
        CONCAT(account.va, received_account.va) AS lu,
        1 AS co,
        9 AS rate,
        CASE WHEN sent_aid.money IN (9000000, 6000000) THEN 100 ELSE 0 END AS ct,
        CASE WHEN sent_aid.money IN (9000000, 6000000) THEN 9 ELSE 0 END AS cc,
        CASE WHEN sent_aid.technology = 100 THEN 9 ELSE 0 END AS tc,
        CASE WHEN sent_aid.technology = 100 THEN 100 ELSE 0 END AS tt
    FROM account
    JOIN cybernations_db.nation nation ON account.nation_id = nation.id
    JOIN cybernations_db.aid sent_aid ON account.nation_id = sent_aid.sending_nation_id
    JOIN cybernations_db.nation received_nation ON sent_aid.receiving_nation_id = received_nation.id
    JOIN account received_account ON sent_aid.receiving_nation_id = received_account.nation_id
    LEFT JOIN transaction txn ON sent_aid.id = txn.aid_id
    WHERE txn.id IS NULL
        AND (account.date_added IS NULL OR sent_aid.date > account.date_added)
        AND (received_account.date_added IS NULL OR sent_aid.date > received_account.date_added)
        AND account.va <> 'H'
        AND received_account.va <> 'H'

    UNION

    -- Process received aid
    SELECT
        recd_aid.id AS aid_id,
        sending_account.nation_id AS sending_nation_id,
        account.nation_id AS received_nation_id,
        NULL AS reason_override,
        CONCAT(sending_account.va, account.va) AS lu,
        1 AS co,
        9 AS rate,
        CASE WHEN recd_aid.money IN (9000000, 6000000) THEN 100 ELSE 0 END AS ct,
        CASE WHEN recd_aid.money IN (9000000, 6000000) THEN 9 ELSE 0 END AS cc,
        CASE WHEN recd_aid.technology = 100 THEN 9 ELSE 0 END AS tc,
        CASE WHEN recd_aid.technology = 100 THEN 100 ELSE 0 END AS tt
    FROM account
    JOIN cybernations_db.nation nation ON account.nation_id = nation.id
    JOIN cybernations_db.aid recd_aid ON account.nation_id = recd_aid.receiving_nation_id
    JOIN cybernations_db.nation sending_nation ON recd_aid.sending_nation_id = sending_nation.id
    JOIN account sending_account ON recd_aid.sending_nation_id = sending_account.nation_id
    LEFT JOIN transaction txn ON recd_aid.id = txn.aid_id
    WHERE txn.id IS NULL
        AND (account.date_added IS NULL OR recd_aid.date > account.date_added)
        AND (sending_account.date_added IS NULL OR recd_aid.date > sending_account.date_added)
        AND account.va <> 'H'
        AND sending_account.va <> 'H';
END$$

-- =============================================================================
-- PROCEDURE: load_todays_lists
-- Purpose: Regenerate message distribution lists with updated priorities
--
-- Flow:
-- 1. Clear and rebuild list_recipient based on balance/activity
-- 2. Update account_list_performance tracking
-- 3. Recalculate priorities based on performance history
-- 4. Reset message send audit flags
-- =============================================================================
CREATE PROCEDURE `load_todays_lists`()
MODIFIES SQL DATA
BEGIN
    -- Rebuild recipient lists
    TRUNCATE TABLE list_recipient;

    INSERT INTO list_recipient (list_id, account_id, priority)
    SELECT
        CASE
            WHEN ((a.va IN ('B', 'D') AND b.Credit = 0) OR (a.va = 'P' AND b.Debt > 0)) THEN 1
            WHEN ((a.va IN ('S', 'N') AND b.Debt = 0) OR (a.va IN ('Q', 'C') AND b.Credit > 0)) THEN 2
            WHEN (a.va = 'F' OR (a.va IN ('S', 'N', 'V') AND b.Debt > 0)) THEN 3
            WHEN (a.va IN ('B', 'W', 'R') AND b.Credit > 0) THEN 4
            ELSE 0
        END AS list_id,
        a.id AS account_id,
        RANK() OVER (ORDER BY a.nation_id) AS priority
    FROM balance b
    JOIN account a ON b.nation_id = a.nation_id
    JOIN aid_activity aa ON a.id = aa.account_id
    WHERE (aa.slots_full - aa.slots_used) > 0
        AND a.va <> 'H';

    -- Track new list memberships
    INSERT INTO account_list_performance (account_id, list_id, day_started)
    SELECT account_id, list_id, CURDATE()
    FROM list_recipient lr
    WHERE NOT EXISTS (
        SELECT 1
        FROM account_list_performance alp
        WHERE alp.account_id = lr.account_id
            AND alp.list_id = lr.list_id
            AND DATE_ADD(alp.day_started, INTERVAL (alp.days_until_removed + 1) DAY) >= CURDATE()
    );

    -- Increment days for continuing memberships
    UPDATE account_list_performance alp
    JOIN list_recipient lr
        ON alp.list_id = lr.list_id
        AND alp.account_id = lr.account_id
        AND DATE_ADD(alp.day_started, INTERVAL (alp.days_until_removed + 1) DAY) = CURDATE()
    SET alp.days_until_removed = alp.days_until_removed + 1;

    -- Recalculate priorities based on performance
    CREATE TEMPORARY TABLE new_priority AS
    SELECT
        lr.account_id,
        lr.list_id,
        RANK() OVER (
            PARTITION BY alp.list_id
            ORDER BY alp.days_until_removed DESC, perf.activity_score, a.nation_id
        ) AS priority
    FROM list_recipient lr
    JOIN account a ON lr.account_id = a.id
    JOIN account_list_performance alp
        ON lr.account_id = alp.account_id
        AND lr.list_id = alp.list_id
        AND DATE_ADD(day_started, INTERVAL days_until_removed DAY) = CURDATE()
    JOIN (
        SELECT account_id, list_id, AVG(days_until_removed) AS activity_score
        FROM account_list_performance
        GROUP BY account_id, list_id
    ) perf ON lr.account_id = perf.account_id AND lr.list_id = perf.list_id;

    UPDATE list_recipient lr
    JOIN new_priority np ON lr.list_id = np.list_id AND lr.account_id = np.account_id
    SET lr.priority = np.priority;

    DROP TEMPORARY TABLE new_priority;

    -- Reset message send tracking
    UPDATE list SET messages_sent_on = NULL, messages_sent_by = NULL;
END$$

-- =============================================================================
-- PROCEDURE: get_pending_credit_per_alliance
-- Purpose: Report on pending credit by alliance and age bracket
--
-- Returns: Alliance name, date range, total credit pending
-- Used for: Alliance leadership credit monitoring
-- =============================================================================
CREATE PROCEDURE `get_pending_credit_per_alliance`()
MODIFIES SQL DATA
BEGIN
    SET @currentDateCst = (SELECT CAST(CAST(CONVERT_TZ(NOW(), '+00:00', '-05:00') AS DATE) AS DATETIME));

    SELECT DateRange, AllianceName, SUM(Credit) AS TotalCredit
    FROM (
        -- Sent aid credit
        SELECT
            CASE
                WHEN aid.date BETWEEN CAST(@currentDateCst - INTERVAL 2 DAY AS DATETIME)
                    AND @currentDateCst - INTERVAL 1 SECOND THEN '0-2 Days'
                WHEN aid.date BETWEEN CAST(@currentDateCst - INTERVAL 5 DAY AS DATETIME)
                    AND @currentDateCst - INTERVAL 2 DAY - INTERVAL 1 SECOND THEN '3-5 Days'
                WHEN aid.date BETWEEN CAST(@currentDateCst - INTERVAL 8 DAY AS DATETIME)
                    AND @currentDateCst - INTERVAL 5 DAY - INTERVAL 1 SECOND THEN '6-8 Days'
                WHEN aid.date < @currentDateCst - INTERVAL 8 DAY - INTERVAL 1 SECOND THEN '9+ Days'
                ELSE 'Query Is Broken'
            END AS DateRange,
            alliance.name AS AllianceName,
            transaction.rate AS Credit
        FROM transaction
        JOIN cybernations_db.aid ON transaction.aid_id = aid.id
        JOIN cybernations_db.alliance ON aid.sending_alliance_id = alliance.id
        JOIN account ON aid.sending_nation_id = account.nation_id
        WHERE aid.status = 1 AND account.va <> 'H'

        UNION ALL

        -- Received aid credit
        SELECT
            CASE
                WHEN aid.date BETWEEN CAST(@currentDateCst - INTERVAL 2 DAY AS DATETIME)
                    AND @currentDateCst - INTERVAL 1 SECOND THEN '0-2 Days'
                WHEN aid.date BETWEEN CAST(@currentDateCst - INTERVAL 5 DAY AS DATETIME)
                    AND @currentDateCst - INTERVAL 2 DAY - INTERVAL 1 SECOND THEN '3-5 Days'
                WHEN aid.date BETWEEN CAST(@currentDateCst - INTERVAL 8 DAY AS DATETIME)
                    AND @currentDateCst - INTERVAL 5 DAY - INTERVAL 1 SECOND THEN '6-8 Days'
                WHEN aid.date < @currentDateCst - INTERVAL 8 DAY - INTERVAL 1 SECOND THEN '9+ Days'
                ELSE 'Query Is Broken'
            END AS DateRange,
            alliance.name AS AllianceName,
            transaction.rate AS Credit
        FROM transaction
        JOIN cybernations_db.aid ON transaction.aid_id = aid.id
        JOIN cybernations_db.alliance ON aid.receiving_alliance_id = alliance.id
        JOIN account ON aid.receiving_nation_id = account.nation_id
        WHERE aid.status = 1 AND account.va <> 'H'
    ) x
    GROUP BY DateRange, AllianceName;
END$$

-- =============================================================================
-- PROCEDURE: search_transactions
-- Purpose: Flexible transaction search with multiple filter options
--
-- Parameters:
--   _type: 0=aid only, 1=manual only, 2=all, NULL=all
--   _account_id: Filter by specific account
--   _sent_by: Nation/alliance name search (sender)
--   _received_by: Nation/alliance name search (receiver)
--   _sent_since: Date filter (start)
--   _sent_until: Date filter (end)
--
-- Returns: Temporary table tmpTxnSearchResults with full transaction details
-- =============================================================================
CREATE PROCEDURE `search_transactions`(
    `_type` INT,
    `_account_id` INT,
    `_sent_by` VARCHAR(50),
    `_received_by` VARCHAR(50),
    `_sent_since` DATETIME,
    `_sent_until` DATETIME
)
MODIFIES SQL DATA
BEGIN
    SET @type = _type;
    SET @account_id = _account_id;
    SET @sent_by = _sent_by;
    SET @received_by = _received_by;
    SET @sent_since = _sent_since;
    SET @sent_until = _sent_until;

    DROP TABLE IF EXISTS tmpTxnSearchResults;

    -- Search by account ID
    IF (@account_id IS NOT NULL) THEN
        CREATE TEMPORARY TABLE tmpTxnSearchResults AS
        SELECT
            txn.id AS Id,
            aid_id AS AidId,
            txn.sending_nation_id AS SentByNationId,
            sending_nation.nation_name AS SentByNationName,
            sending_nation.ruler_name AS SentByRulerName,
            sending_alliance.name AS SentByAllianceName,
            txn.receiving_nation_id AS ReceivedByNationId,
            receiving_nation.nation_name AS ReceivedByNationName,
            receiving_nation.ruler_name AS ReceivedByRulerName,
            receiving_alliance.name AS ReceivedByAllianceName,
            COALESCE(aid.status, 4) AS Status,
            aid.money AS Money,
            aid.technology AS Technology,
            aid.soldiers AS Soldiers,
            COALESCE(txn.reason_override, aid.reason) AS Reason,
            aid.date AS StartsOn,
            txn.lu AS Lu,
            txn.co AS Classification,
            txn.rate AS Rate,
            txn.ct AS CashMovedTechCredit,
            txn.cc AS CashMovedCashCredit,
            txn.tc AS TechMovedCashCredit,
            txn.tt AS TechMovedTechCredit
        FROM transaction txn
        LEFT JOIN cybernations_db.aid ON txn.aid_id = aid.id
        LEFT JOIN cybernations_db.nation sending_nation ON txn.sending_nation_id = sending_nation.id
        LEFT JOIN account sending_account ON sending_nation.id = sending_account.nation_id
        LEFT JOIN cybernations_db.alliance sending_alliance ON sending_nation.alliance_id = sending_alliance.id
        LEFT JOIN cybernations_db.nation receiving_nation ON txn.receiving_nation_id = receiving_nation.id
        LEFT JOIN account receiving_account ON receiving_nation.id = receiving_account.nation_id
        LEFT JOIN cybernations_db.alliance receiving_alliance ON receiving_nation.alliance_id = receiving_alliance.id
        WHERE sending_account.id = @account_id OR receiving_account.id = @account_id;

    -- Search by nation/alliance (same for sent/received)
    ELSEIF (@sent_by IS NOT NULL AND @received_by IS NOT NULL AND @sent_by = @received_by) THEN
        CREATE TEMPORARY TABLE tmpTxnSearchResults AS
        SELECT
            txn.id AS Id, aid_id AS AidId,
            txn.sending_nation_id AS SentByNationId,
            sending_nation.nation_name AS SentByNationName,
            sending_nation.ruler_name AS SentByRulerName,
            sending_alliance.name AS SentByAllianceName,
            txn.receiving_nation_id AS ReceivedByNationId,
            receiving_nation.nation_name AS ReceivedByNationName,
            receiving_nation.ruler_name AS ReceivedByRulerName,
            receiving_alliance.name AS ReceivedByAllianceName,
            COALESCE(aid.status, 4) AS Status,
            aid.money AS Money, aid.technology AS Technology, aid.soldiers AS Soldiers,
            COALESCE(txn.reason_override, aid.reason) AS Reason,
            aid.date AS StartsOn, txn.lu AS Lu, txn.co AS Classification, txn.rate AS Rate,
            txn.ct AS CashMovedTechCredit, txn.cc AS CashMovedCashCredit,
            txn.tc AS TechMovedCashCredit, txn.tt AS TechMovedTechCredit
        FROM transaction txn
        LEFT JOIN cybernations_db.aid ON txn.aid_id = aid.id
        LEFT JOIN cybernations_db.nation sending_nation ON txn.sending_nation_id = sending_nation.id
        LEFT JOIN cybernations_db.alliance sending_alliance ON sending_nation.alliance_id = sending_alliance.id
        LEFT JOIN cybernations_db.nation receiving_nation ON txn.receiving_nation_id = receiving_nation.id
        LEFT JOIN cybernations_db.alliance receiving_alliance ON receiving_nation.alliance_id = receiving_alliance.id
        WHERE (@type IS NULL OR @type = 0 AND aid.id IS NOT NULL OR @type = 1 AND aid.id IS NULL OR @type = 2)
            AND ((
                @sent_by IS NULL
                OR sending_nation.nation_name LIKE CONCAT('%', @sent_by, '%')
                OR sending_nation.ruler_name LIKE CONCAT('%', @sent_by, '%')
                OR sending_alliance.name LIKE CONCAT('%', @sent_by, '%')
            ) OR (
                @received_by IS NULL
                OR receiving_nation.nation_name LIKE CONCAT('%', @received_by, '%')
                OR receiving_nation.ruler_name LIKE CONCAT('%', @received_by, '%')
                OR receiving_alliance.name LIKE CONCAT('%', @received_by, '%')
            ))
            AND (@sent_until IS NULL OR aid.date IS NULL OR aid.date <= @sent_until)
            AND (@sent_since IS NULL OR aid.date IS NULL OR aid.date >= @sent_since);

    -- Search by distinct sent/received
    ELSE
        CREATE TEMPORARY TABLE tmpTxnSearchResults AS
        SELECT
            txn.id AS Id, aid_id AS AidId,
            txn.sending_nation_id AS SentByNationId,
            sending_nation.nation_name AS SentByNationName,
            sending_nation.ruler_name AS SentByRulerName,
            sending_alliance.name AS SentByAllianceName,
            txn.receiving_nation_id AS ReceivedByNationId,
            receiving_nation.nation_name AS ReceivedByNationName,
            receiving_nation.ruler_name AS ReceivedByRulerName,
            receiving_alliance.name AS ReceivedByAllianceName,
            COALESCE(aid.status, 4) AS Status,
            aid.money AS Money, aid.technology AS Technology, aid.soldiers AS Soldiers,
            COALESCE(txn.reason_override, aid.reason) AS Reason,
            aid.date AS StartsOn, txn.lu AS Lu, txn.co AS Classification, txn.rate AS Rate,
            txn.ct AS CashMovedTechCredit, txn.cc AS CashMovedCashCredit,
            txn.tc AS TechMovedCashCredit, txn.tt AS TechMovedTechCredit
        FROM transaction txn
        LEFT JOIN cybernations_db.aid ON txn.aid_id = aid.id
        LEFT JOIN cybernations_db.nation sending_nation ON txn.sending_nation_id = sending_nation.id
        LEFT JOIN cybernations_db.alliance sending_alliance ON sending_nation.alliance_id = sending_alliance.id
        LEFT JOIN cybernations_db.nation receiving_nation ON txn.receiving_nation_id = receiving_nation.id
        LEFT JOIN cybernations_db.alliance receiving_alliance ON receiving_nation.alliance_id = receiving_alliance.id
        WHERE (@type IS NULL OR @type = 0 AND aid.id IS NOT NULL OR @type = 1 AND aid.id IS NULL OR @type = 2)
            AND (
                @sent_by IS NULL
                OR sending_nation.nation_name LIKE CONCAT('%', @sent_by, '%')
                OR sending_nation.ruler_name LIKE CONCAT('%', @sent_by, '%')
                OR sending_alliance.name LIKE CONCAT('%', @sent_by, '%')
            )
            AND (
                @received_by IS NULL
                OR receiving_nation.nation_name LIKE CONCAT('%', @received_by, '%')
                OR receiving_nation.ruler_name LIKE CONCAT('%', @received_by, '%')
                OR receiving_alliance.name LIKE CONCAT('%', @received_by, '%')
            )
            AND (@sent_until IS NULL OR aid.date IS NULL OR aid.date <= @sent_until)
            AND (@sent_since IS NULL OR aid.date IS NULL OR aid.date >= @sent_since);
    END IF;
END$$

DELIMITER ;
