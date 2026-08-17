-- =============================================================================
-- V3__views.sql
-- Database views for transaction and balance calculations
--
-- These views provide aggregated data for the VEP transaction system
-- =============================================================================

USE `cybernations_db`;

-- -----------------------------------------------------------------------------
-- VIEW: sent_txn
-- Purpose: Aggregate sent transactions by nation and VA type
-- Used by: balance view for credit/debt calculation
-- -----------------------------------------------------------------------------
CREATE OR REPLACE ALGORITHM=UNDEFINED
DEFINER=`base_admin`@`%`
SQL SECURITY DEFINER
VIEW `sent_txn` AS
SELECT
    `vep_db`.`transaction`.`sending_nation_id` AS `nation_id`,
    LEFT(`vep_db`.`transaction`.`lu`, 1) AS `va`,
    SUM(`vep_db`.`transaction`.`ct`) AS `ct`,
    SUM(`vep_db`.`transaction`.`cc`) AS `cc`,
    SUM(`vep_db`.`transaction`.`tc`) AS `tc`,
    SUM(`vep_db`.`transaction`.`tt`) AS `tt`
FROM `vep_db`.`transaction`
GROUP BY
    `vep_db`.`transaction`.`sending_nation_id`,
    LEFT(`vep_db`.`transaction`.`lu`, 1);

-- -----------------------------------------------------------------------------
-- VIEW: recd_txn
-- Purpose: Aggregate received transactions by nation and VA type
-- Used by: balance view for credit/debt calculation
-- -----------------------------------------------------------------------------
CREATE OR REPLACE ALGORITHM=UNDEFINED
DEFINER=`base_admin`@`%`
SQL SECURITY DEFINER
VIEW `recd_txn` AS
SELECT
    `vep_db`.`transaction`.`receiving_nation_id` AS `nation_id`,
    RIGHT(`vep_db`.`transaction`.`lu`, 1) AS `va`,
    SUM(`vep_db`.`transaction`.`ct`) AS `ct`,
    SUM(`vep_db`.`transaction`.`cc`) AS `cc`,
    SUM(`vep_db`.`transaction`.`tc`) AS `tc`,
    SUM(`vep_db`.`transaction`.`tt`) AS `tt`
FROM `vep_db`.`transaction`
GROUP BY
    `vep_db`.`transaction`.`receiving_nation_id`,
    RIGHT(`vep_db`.`transaction`.`lu`, 1);

-- =============================================================================

USE `vep_db`;

-- -----------------------------------------------------------------------------
-- VIEW: balance
-- Purpose: Calculate credit/debt balances for each account
-- Logic: Balances are calculated based on VA type and transaction direction
--   - Different VA types have different credit/debt calculation rules
--   - Sent and received transactions are aggregated from sent_txn/recd_txn views
-- -----------------------------------------------------------------------------
CREATE OR REPLACE ALGORITHM=UNDEFINED
DEFINER=`base_admin`@`%`
SQL SECURITY DEFINER
VIEW `balance` AS
SELECT
    `account`.`nation_id` AS `nation_id`,
    `account`.`psw` AS `psw`,
    `account`.`va` AS `va`,
    -- Credit calculation based on VA type
    (CASE
        WHEN (`account`.`va` IN ('B','W')) THEN
            (CASE
                WHEN ((COALESCE(`st`.`ct`,0) - COALESCE(`rt`.`tt`,0)) < 0) THEN 0
                ELSE (COALESCE(`st`.`ct`,0) - COALESCE(`rt`.`tt`,0))
            END)
        WHEN (`account`.`va` IN ('S','N','Q')) THEN
            (CASE
                WHEN ((COALESCE(`st`.`tc`,0) - COALESCE(`rt`.`cc`,0)) < 0) THEN 0
                ELSE (COALESCE(`st`.`tc`,0) - COALESCE(`rt`.`cc`,0))
            END)
        WHEN (`account`.`va` IN ('C','R')) THEN 1
        ELSE 0
    END) AS `Credit`,
    -- Debt calculation based on VA type
    (CASE
        WHEN (`account`.`va` IN ('B','P')) THEN
            (CASE
                WHEN ((COALESCE(`rt`.`tc`,0) - COALESCE(`st`.`cc`,0)) < 0) THEN 0
                ELSE (COALESCE(`rt`.`tc`,0) - COALESCE(`st`.`cc`,0))
            END)
        WHEN (`account`.`va` IN ('S','N','V')) THEN
            (CASE
                WHEN ((COALESCE(`rt`.`ct`,0) - COALESCE(`st`.`tt`,0)) < 0) THEN 0
                ELSE (COALESCE(`rt`.`ct`,0) - COALESCE(`st`.`tt`,0))
            END)
        WHEN (`account`.`va` IN ('F','D')) THEN 1
        ELSE 0
    END) AS `Debt`,
    -- Transaction detail columns for transparency
    COALESCE(`st`.`ct`,0) AS `ctB`,
    COALESCE(`rt`.`ct`,0) AS `ctS`,
    COALESCE(`st`.`cc`,0) AS `ccB`,
    COALESCE(`rt`.`cc`,0) AS `ccS`,
    COALESCE(`st`.`tc`,0) AS `tcB`,
    COALESCE(`rt`.`tc`,0) AS `tcS`,
    COALESCE(`st`.`tt`,0) AS `ttB`,
    COALESCE(`rt`.`tt`,0) AS `ttS`
FROM `account`
LEFT JOIN `cybernations_db`.`sent_txn` `st`
    ON (`account`.`nation_id` = `st`.`nation_id`
    AND `account`.`va` = `st`.`va`)
LEFT JOIN `cybernations_db`.`recd_txn` `rt`
    ON (`account`.`nation_id` = `rt`.`nation_id`
    AND `account`.`va` = `rt`.`va`);

-- -----------------------------------------------------------------------------
-- VIEW: aid_activity
-- Purpose: Calculate aid slot usage for each account
-- Logic: Compares active aid (sent + received in last 10 days) against
--        total slots available (base 4 + FM + DRA bonuses)
-- -----------------------------------------------------------------------------
CREATE OR REPLACE ALGORITHM=UNDEFINED
DEFINER=`base_admin`@`%`
SQL SECURITY DEFINER
VIEW `aid_activity` AS
SELECT
    `account`.`id` AS `account_id`,
    `nation`.`id` AS `nation_id`,
    (COALESCE(`sent_aid_active`.`count`,0) + COALESCE(`recd_aid_active`.`count`,0)) AS `slots_used`,
    ((4 + `account`.`fm`) + `account`.`dra`) AS `slots_full`
FROM `account`
JOIN `cybernations_db`.`nation` ON (`account`.`nation_id` = `nation`.`id`)
LEFT JOIN (
    -- Count active sent aid
    SELECT
        `aid`.`sending_nation_id` AS `nation_id`,
        COUNT(1) AS `count`
    FROM `cybernations_db`.`aid`
    WHERE (CAST(`aid`.`date` AS DATE) > (CAST(CONVERT_TZ(NOW(),'+00:00','-05:00') AS DATE) - INTERVAL 10 DAY))
        AND (`aid`.`status` <> 3)
    GROUP BY `aid`.`sending_nation_id`
) `sent_aid_active` ON (`nation`.`id` = `sent_aid_active`.`nation_id`)
LEFT JOIN (
    -- Count active received aid
    SELECT
        `aid`.`receiving_nation_id` AS `nation_id`,
        COUNT(1) AS `count`
    FROM `cybernations_db`.`aid`
    WHERE (CAST(`aid`.`date` AS DATE) > (CAST(CONVERT_TZ(NOW(),'+00:00','-05:00') AS DATE) - INTERVAL 10 DAY))
        AND (`aid`.`status` <> 3)
    GROUP BY `aid`.`receiving_nation_id`
) `recd_aid_active` ON (`nation`.`id` = `recd_aid_active`.`nation_id`);
