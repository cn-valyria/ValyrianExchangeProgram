-- =============================================================================
-- V2__triggers_and_audit.sql
-- Audit triggers for change tracking
--
-- These triggers automatically record changes to core tables (nation, alliance,
-- aid, war) by inserting records into their respective audit tables.
-- =============================================================================

USE `cybernations_db`;

-- -----------------------------------------------------------------------------
-- NATION TRIGGERS
-- Track updates and deletes to the nation table
-- -----------------------------------------------------------------------------

DELIMITER $$

CREATE TRIGGER `tr_nations_update`
AFTER UPDATE ON `nation`
FOR EACH ROW
BEGIN
    INSERT INTO `audit_nation`(
        `change_timestamp`,
        `change_code`,
        `nation_id`,
        `nation_name`,
        `ruler_name`,
        `alliance_id`,
        `alliance_date`,
        `alliance_status`,
        `government_type`,
        `religion`,
        `team`,
        `created`,
        `technology`,
        `infrastructure`,
        `base_land`,
        `war_status`,
        `resource_1`,
        `resource_2`,
        `votes`,
        `strength`,
        `defcon`,
        `base_soldiers`,
        `tanks`,
        `cruise_missiles`,
        `nukes`,
        `recent_activity`,
        `connected_resource_1`,
        `connected_resource_2`,
        `connected_resource_3`,
        `connected_resource_4`,
        `connected_resource_5`,
        `connected_resource_6`,
        `connected_resource_7`,
        `connected_resource_8`,
        `connected_resource_9`,
        `connected_resource_10`)
    VALUES (
        CURRENT_TIMESTAMP,
        'U',
        OLD.`id`,
        OLD.`nation_name`,
        OLD.`ruler_name`,
        OLD.`alliance_id`,
        OLD.`alliance_date`,
        OLD.`alliance_status`,
        OLD.`government_type`,
        OLD.`religion`,
        OLD.`team`,
        OLD.`created`,
        OLD.`technology`,
        OLD.`infrastructure`,
        OLD.`base_land`,
        OLD.`war_status`,
        OLD.`resource_1`,
        OLD.`resource_2`,
        OLD.`votes`,
        OLD.`strength`,
        OLD.`defcon`,
        OLD.`base_soldiers`,
        OLD.`tanks`,
        OLD.`cruise_missiles`,
        OLD.`nukes`,
        OLD.`recent_activity`,
        OLD.`connected_resource_1`,
        OLD.`connected_resource_2`,
        OLD.`connected_resource_3`,
        OLD.`connected_resource_4`,
        OLD.`connected_resource_5`,
        OLD.`connected_resource_6`,
        OLD.`connected_resource_7`,
        OLD.`connected_resource_8`,
        OLD.`connected_resource_9`,
        OLD.`connected_resource_10`
    );
END$$

CREATE TRIGGER `tr_nations_delete`
AFTER DELETE ON `nation`
FOR EACH ROW
BEGIN
    INSERT INTO `audit_nation`(
        `change_timestamp`,
        `change_code`,
        `nation_id`,
        `nation_name`,
        `ruler_name`,
        `alliance_id`,
        `alliance_date`,
        `alliance_status`,
        `government_type`,
        `religion`,
        `team`,
        `created`,
        `technology`,
        `infrastructure`,
        `base_land`,
        `war_status`,
        `resource_1`,
        `resource_2`,
        `votes`,
        `strength`,
        `defcon`,
        `base_soldiers`,
        `tanks`,
        `cruise_missiles`,
        `nukes`,
        `recent_activity`,
        `connected_resource_1`,
        `connected_resource_2`,
        `connected_resource_3`,
        `connected_resource_4`,
        `connected_resource_5`,
        `connected_resource_6`,
        `connected_resource_7`,
        `connected_resource_8`,
        `connected_resource_9`,
        `connected_resource_10`)
    VALUES (
        CURRENT_TIMESTAMP,
        'D',
        OLD.`id`,
        OLD.`nation_name`,
        OLD.`ruler_name`,
        OLD.`alliance_id`,
        OLD.`alliance_date`,
        OLD.`alliance_status`,
        OLD.`government_type`,
        OLD.`religion`,
        OLD.`team`,
        OLD.`created`,
        OLD.`technology`,
        OLD.`infrastructure`,
        OLD.`base_land`,
        OLD.`war_status`,
        OLD.`resource_1`,
        OLD.`resource_2`,
        OLD.`votes`,
        OLD.`strength`,
        OLD.`defcon`,
        OLD.`base_soldiers`,
        OLD.`tanks`,
        OLD.`cruise_missiles`,
        OLD.`nukes`,
        OLD.`recent_activity`,
        OLD.`connected_resource_1`,
        OLD.`connected_resource_2`,
        OLD.`connected_resource_3`,
        OLD.`connected_resource_4`,
        OLD.`connected_resource_5`,
        OLD.`connected_resource_6`,
        OLD.`connected_resource_7`,
        OLD.`connected_resource_8`,
        OLD.`connected_resource_9`,
        OLD.`connected_resource_10`
    );
END$$

-- -----------------------------------------------------------------------------
-- ALLIANCE TRIGGERS
-- Track updates and deletes to the alliance table
-- -----------------------------------------------------------------------------

CREATE TRIGGER `tr_alliance_update`
AFTER UPDATE ON `alliance`
FOR EACH ROW
BEGIN
    INSERT INTO `audit_alliance`(
        `change_timestamp`,
        `change_code`,
        `alliance_id`,
        `name`,
        `updated`,
        `total_nations`,
        `active_nations`,
        `percent_active`,
        `strength`,
        `average_strength`,
        `score`,
        `land`,
        `infrastructure`,
        `technology`,
        `war`,
        `peace`,
        `soldiers`,
        `tanks`,
        `cruise`,
        `nukes`,
        `aircraft`,
        `navy`,
        `anarchy`
    ) VALUES (
        CURRENT_TIMESTAMP,
        'U',
        OLD.`id`,
        OLD.`name`,
        OLD.`updated`,
        OLD.`total_nations`,
        OLD.`active_nations`,
        OLD.`percent_active`,
        OLD.`strength`,
        OLD.`average_strength`,
        OLD.`score`,
        OLD.`land`,
        OLD.`infrastructure`,
        OLD.`technology`,
        OLD.`war`,
        OLD.`peace`,
        OLD.`soldiers`,
        OLD.`tanks`,
        OLD.`cruise`,
        OLD.`nukes`,
        OLD.`aircraft`,
        OLD.`navy`,
        OLD.`anarchy`
    );
END$$

CREATE TRIGGER `tr_alliance_delete`
AFTER DELETE ON `alliance`
FOR EACH ROW
BEGIN
    INSERT INTO `audit_alliance`(
        `change_timestamp`,
        `change_code`,
        `alliance_id`,
        `name`,
        `updated`,
        `total_nations`,
        `active_nations`,
        `percent_active`,
        `strength`,
        `average_strength`,
        `score`,
        `land`,
        `infrastructure`,
        `technology`,
        `war`,
        `peace`,
        `soldiers`,
        `tanks`,
        `cruise`,
        `nukes`,
        `aircraft`,
        `navy`,
        `anarchy`
    ) VALUES (
        CURRENT_TIMESTAMP,
        'D',
        OLD.`id`,
        OLD.`name`,
        OLD.`updated`,
        OLD.`total_nations`,
        OLD.`active_nations`,
        OLD.`percent_active`,
        OLD.`strength`,
        OLD.`average_strength`,
        OLD.`score`,
        OLD.`land`,
        OLD.`infrastructure`,
        OLD.`technology`,
        OLD.`war`,
        OLD.`peace`,
        OLD.`soldiers`,
        OLD.`tanks`,
        OLD.`cruise`,
        OLD.`nukes`,
        OLD.`aircraft`,
        OLD.`navy`,
        OLD.`anarchy`
    );
END$$

-- -----------------------------------------------------------------------------
-- AID TRIGGERS
-- Track updates and deletes to the aid table
-- -----------------------------------------------------------------------------

CREATE TRIGGER `tr_aid_update`
AFTER UPDATE ON `aid`
FOR EACH ROW
BEGIN
    INSERT INTO `audit_aid`(
        `change_timestamp`,
        `change_code`,
        `aid_id`,
        `sending_nation_id`,
        `sending_alliance_id`,
        `sending_team`,
        `receiving_nation_id`,
        `receiving_alliance_id`,
        `receiving_team`,
        `status`,
        `money`,
        `technology`,
        `soldiers`,
        `date`,
        `reason`
    ) VALUES (
        CURRENT_TIMESTAMP,
        'U',
        OLD.`id`,
        OLD.`sending_nation_id`,
        OLD.`sending_alliance_id`,
        OLD.`sending_team`,
        OLD.`receiving_nation_id`,
        OLD.`receiving_alliance_id`,
        OLD.`receiving_team`,
        OLD.`status`,
        OLD.`money`,
        OLD.`technology`,
        OLD.`soldiers`,
        OLD.`date`,
        OLD.`reason`
    );
END$$

CREATE TRIGGER `tr_aid_delete`
AFTER DELETE ON `aid`
FOR EACH ROW
BEGIN
    INSERT INTO `audit_aid`(
        `change_timestamp`,
        `change_code`,
        `aid_id`,
        `sending_nation_id`,
        `sending_alliance_id`,
        `sending_team`,
        `receiving_nation_id`,
        `receiving_alliance_id`,
        `receiving_team`,
        `status`,
        `money`,
        `technology`,
        `soldiers`,
        `date`,
        `reason`
    ) VALUES (
        CURRENT_TIMESTAMP,
        'D',
        OLD.`id`,
        OLD.`sending_nation_id`,
        OLD.`sending_alliance_id`,
        OLD.`sending_team`,
        OLD.`receiving_nation_id`,
        OLD.`receiving_alliance_id`,
        OLD.`receiving_team`,
        OLD.`status`,
        OLD.`money`,
        OLD.`technology`,
        OLD.`soldiers`,
        OLD.`date`,
        OLD.`reason`
    );
END$$

-- -----------------------------------------------------------------------------
-- WAR TRIGGERS
-- Track updates and deletes to the war table
-- -----------------------------------------------------------------------------

CREATE TRIGGER `tr_war_update`
AFTER UPDATE ON `war`
FOR EACH ROW
BEGIN
    INSERT INTO `audit_war` (
        `change_timestamp`,
        `change_code`,
        `war_id`,
        `attacking_nation_id`,
        `attacking_alliance_id`,
        `attacking_team`,
        `defending_nation_id`,
        `defending_alliance_id`,
        `defending_team`,
        `war_status`,
        `begin_date`,
        `end_date`,
        `reason`,
        `destruction`,
        `attack_percent`,
        `defend_percent`
    ) VALUES (
        CURRENT_TIMESTAMP,
        'U',
        OLD.`id`,
        OLD.`attacking_nation_id`,
        OLD.`attacking_alliance_id`,
        OLD.`attacking_team`,
        OLD.`defending_nation_id`,
        OLD.`defending_alliance_id`,
        OLD.`defending_team`,
        OLD.`war_status`,
        OLD.`begin_date`,
        OLD.`end_date`,
        OLD.`reason`,
        OLD.`destruction`,
        OLD.`attack_percent`,
        OLD.`defend_percent`
    );
END$$

CREATE TRIGGER `tr_war_delete`
AFTER DELETE ON `war`
FOR EACH ROW
BEGIN
    INSERT INTO `audit_war` (
        `change_timestamp`,
        `change_code`,
        `war_id`,
        `attacking_nation_id`,
        `attacking_alliance_id`,
        `attacking_team`,
        `defending_nation_id`,
        `defending_alliance_id`,
        `defending_team`,
        `war_status`,
        `begin_date`,
        `end_date`,
        `reason`,
        `destruction`,
        `attack_percent`,
        `defend_percent`
    ) VALUES (
        CURRENT_TIMESTAMP,
        'D',
        OLD.`id`,
        OLD.`attacking_nation_id`,
        OLD.`attacking_alliance_id`,
        OLD.`attacking_team`,
        OLD.`defending_nation_id`,
        OLD.`defending_alliance_id`,
        OLD.`defending_team`,
        OLD.`war_status`,
        OLD.`begin_date`,
        OLD.`end_date`,
        OLD.`reason`,
        OLD.`destruction`,
        OLD.`attack_percent`,
        OLD.`defend_percent`
    );
END$$

DELIMITER ;
