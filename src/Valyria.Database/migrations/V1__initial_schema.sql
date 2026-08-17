-- =============================================================================
-- V1__initial_schema.sql
-- Initial database schema for Valyria application
--
-- This migration creates two databases:
-- 1. cybernations_db - Core game data (nations, alliances, aid, wars)
-- 2. vep_db - Application data (accounts, transactions, lists)
-- =============================================================================

-- =============================================================================
-- DATABASE: cybernations_db
-- Purpose: Stores core Cybernations game data from daily file imports
-- =============================================================================

CREATE DATABASE IF NOT EXISTS `cybernations_db`
    DEFAULT CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci
    DEFAULT ENCRYPTION='N';

USE `cybernations_db`;

-- -----------------------------------------------------------------------------
-- TABLE: todays_file_nation
-- Purpose: Staging table for daily nation file imports
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `todays_file_nation` (
    `nation_id` int NOT NULL,
    `nation_name` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
    `ruler_name` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
    `alliance_id` int DEFAULT NULL,
    `alliance_date` datetime NOT NULL,
    `alliance_status` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `government_type` int NOT NULL,
    `religion` int NOT NULL,
    `team` int NOT NULL,
    `created` datetime NOT NULL,
    `technology` float NOT NULL,
    `infrastructure` float NOT NULL,
    `base_land` float NOT NULL,
    `war_status` int NOT NULL,
    `votes` int NOT NULL,
    `strength` float NOT NULL,
    `defcon` int NOT NULL,
    `base_soldiers` int NOT NULL,
    `tanks` int NOT NULL,
    `cruise_missiles` int NOT NULL,
    `nukes` int NOT NULL,
    `recent_activity` int NOT NULL,
    `connected_resource_1` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `connected_resource_2` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `connected_resource_3` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `connected_resource_4` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `connected_resource_5` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `connected_resource_6` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `connected_resource_7` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `connected_resource_8` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `connected_resource_9` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `connected_resource_10` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `file_name` varchar(500) COLLATE utf8mb4_unicode_ci NOT NULL,
    `resource_1` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `resource_2` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    PRIMARY KEY (`nation_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- -----------------------------------------------------------------------------
-- TABLE: todays_file_alliance
-- Purpose: Staging table for daily alliance file imports
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `todays_file_alliance` (
    `alliance_id` int NOT NULL,
    `name` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
    `updated` datetime DEFAULT NULL,
    `total_nations` int NOT NULL,
    `active_nations` int NOT NULL,
    `percent_active` int NOT NULL,
    `strength` int NOT NULL,
    `average_strength` int NOT NULL,
    `score` float NOT NULL,
    `land` int NOT NULL,
    `infrastructure` int NOT NULL,
    `technology` int NOT NULL,
    `war` int NOT NULL,
    `peace` int NOT NULL,
    `soldiers` int NOT NULL,
    `tanks` int NOT NULL,
    `cruise` int NOT NULL,
    `nukes` int NOT NULL,
    `aircraft` int NOT NULL,
    `navy` int NOT NULL,
    `anarchy` int NOT NULL,
    `file_name` varchar(500) COLLATE utf8mb4_unicode_ci NOT NULL,
    PRIMARY KEY (`alliance_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- -----------------------------------------------------------------------------
-- TABLE: todays_file_aid
-- Purpose: Staging table for daily aid file imports
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `todays_file_aid` (
    `aid_id` int NOT NULL,
    `sending_nation_id` int NOT NULL,
    `sending_alliance_id` int NOT NULL,
    `sending_team` int NOT NULL,
    `receiving_nation_id` int NOT NULL,
    `receiving_alliance_id` int NOT NULL,
    `receiving_team` int NOT NULL,
    `status` int NOT NULL,
    `money` int NOT NULL,
    `technology` int NOT NULL,
    `soldiers` int NOT NULL,
    `date` datetime DEFAULT NULL,
    `reason` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `file_name` varchar(500) COLLATE utf8mb4_unicode_ci NOT NULL,
    PRIMARY KEY (`aid_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- -----------------------------------------------------------------------------
-- TABLE: todays_file_war
-- Purpose: Staging table for daily war file imports
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `todays_file_war` (
    `war_id` int NOT NULL,
    `attacking_nation_id` int NOT NULL,
    `attacking_alliance_id` int NOT NULL,
    `attacking_team` int NOT NULL,
    `defending_nation_id` int NOT NULL,
    `defending_alliance_id` int NOT NULL,
    `defending_team` int NOT NULL,
    `war_status` int NOT NULL,
    `begin_date` datetime DEFAULT NULL,
    `end_date` datetime DEFAULT NULL,
    `reason` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `destruction` float NOT NULL,
    `attack_percent` int NOT NULL,
    `defend_percent` int NOT NULL,
    `file_name` varchar(500) COLLATE utf8mb4_unicode_ci NOT NULL,
    PRIMARY KEY (`war_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- -----------------------------------------------------------------------------
-- TABLE: nation
-- Purpose: Current state of all nations (updated via stored procedures)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `nation` (
    `id` int NOT NULL,
    `nation_name` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
    `ruler_name` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
    `alliance_id` int DEFAULT NULL,
    `alliance_date` datetime NOT NULL,
    `alliance_status` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `government_type` int NOT NULL,
    `religion` int NOT NULL,
    `team` int NOT NULL,
    `created` datetime NOT NULL,
    `technology` float NOT NULL,
    `infrastructure` float NOT NULL,
    `base_land` float NOT NULL,
    `war_status` int NOT NULL,
    `votes` int NOT NULL,
    `strength` float NOT NULL,
    `defcon` int NOT NULL,
    `base_soldiers` int NOT NULL,
    `tanks` int NOT NULL,
    `cruise_missiles` int NOT NULL,
    `nukes` int NOT NULL,
    `recent_activity` int NOT NULL,
    `connected_resource_1` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `connected_resource_2` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `connected_resource_3` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `connected_resource_4` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `connected_resource_5` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `connected_resource_6` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `connected_resource_7` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `connected_resource_8` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `connected_resource_9` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `connected_resource_10` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `resource_1` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `resource_2` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `audit_updated_on` datetime DEFAULT NULL,
    `audit_updated_by` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
    PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- -----------------------------------------------------------------------------
-- TABLE: alliance
-- Purpose: Current state of all alliances (updated via stored procedures)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `alliance` (
    `id` int NOT NULL,
    `name` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
    `updated` datetime DEFAULT NULL,
    `total_nations` int NOT NULL,
    `active_nations` int NOT NULL,
    `percent_active` int NOT NULL,
    `strength` int NOT NULL,
    `average_strength` int NOT NULL,
    `score` float NOT NULL,
    `land` int NOT NULL,
    `infrastructure` int NOT NULL,
    `technology` int NOT NULL,
    `war` int NOT NULL,
    `peace` int NOT NULL,
    `soldiers` int NOT NULL,
    `tanks` int NOT NULL,
    `cruise` int NOT NULL,
    `nukes` int NOT NULL,
    `aircraft` int NOT NULL,
    `navy` int NOT NULL,
    `anarchy` int NOT NULL,
    `audit_updated_on` datetime DEFAULT NULL,
    `audit_updated_by` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
    PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- -----------------------------------------------------------------------------
-- TABLE: aid
-- Purpose: Current state of all aid transactions (updated via stored procedures)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `aid` (
    `id` int NOT NULL,
    `sending_nation_id` int NOT NULL,
    `sending_alliance_id` int NOT NULL,
    `sending_team` int NOT NULL,
    `receiving_nation_id` int NOT NULL,
    `receiving_alliance_id` int NOT NULL,
    `receiving_team` int NOT NULL,
    `status` int NOT NULL,
    `money` int NOT NULL,
    `technology` int NOT NULL,
    `soldiers` int NOT NULL,
    `date` datetime DEFAULT NULL,
    `reason` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `audit_updated_on` datetime DEFAULT NULL,
    `audit_updated_by` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
    PRIMARY KEY (`id`),
    KEY `sending_nation_id` (`sending_nation_id`),
    KEY `receiving_nation_id` (`receiving_nation_id`),
    KEY `idx_aid_sender_status_date` (`sending_nation_id`,`status`,`date`),
    KEY `idx_aid_receiver_status_date` (`receiving_nation_id`,`status`,`date`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- -----------------------------------------------------------------------------
-- TABLE: war
-- Purpose: Current state of all wars (updated via stored procedures)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `war` (
    `id` int NOT NULL,
    `attacking_nation_id` int NOT NULL,
    `attacking_alliance_id` int NOT NULL,
    `attacking_team` int NOT NULL,
    `defending_nation_id` int NOT NULL,
    `defending_alliance_id` int NOT NULL,
    `defending_team` int NOT NULL,
    `war_status` int NOT NULL,
    `begin_date` datetime DEFAULT NULL,
    `end_date` datetime DEFAULT NULL,
    `reason` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `destruction` float NOT NULL,
    `attack_percent` int NOT NULL,
    `defend_percent` int NOT NULL,
    `audit_updated_on` datetime DEFAULT NULL,
    `audit_updated_by` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
    PRIMARY KEY (`id`),
    KEY `idx_war_attacker_defender_status_end` (`attacking_nation_id`,`defending_nation_id`,`war_status`,`end_date`),
    KEY `idx_war_defender_attacker_status_end` (`defending_nation_id`,`attacking_nation_id`,`war_status`,`end_date`),
    KEY `idx_war_attacker_status_end` (`attacking_nation_id`,`war_status`,`end_date`),
    KEY `idx_war_defender_status_end` (`defending_nation_id`,`war_status`,`end_date`),
    KEY `idx_war_attacker_begin` (`attacking_nation_id`,`begin_date`),
    KEY `idx_war_defender_begin` (`defending_nation_id`,`begin_date`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- -----------------------------------------------------------------------------
-- AUDIT TABLES
-- Purpose: Historical change tracking for nations, alliances, aid, and wars
-- Populated automatically via triggers
-- -----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS `audit_nation` (
    `id` int NOT NULL AUTO_INCREMENT,
    `change_timestamp` datetime NOT NULL,
    `change_code` char(1) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'U=Update, D=Delete',
    `nation_id` int NOT NULL,
    `nation_name` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
    `ruler_name` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
    `alliance_id` int DEFAULT NULL,
    `alliance_date` datetime NOT NULL,
    `alliance_status` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `government_type` int NOT NULL,
    `religion` int NOT NULL,
    `team` int NOT NULL,
    `created` datetime NOT NULL,
    `technology` float NOT NULL,
    `infrastructure` float NOT NULL,
    `base_land` float NOT NULL,
    `war_status` int DEFAULT NULL,
    `votes` int NOT NULL,
    `strength` float NOT NULL,
    `defcon` int NOT NULL,
    `base_soldiers` int NOT NULL,
    `tanks` int NOT NULL,
    `cruise_missiles` int NOT NULL,
    `nukes` int NOT NULL,
    `recent_activity` int NOT NULL,
    `connected_resource_1` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `connected_resource_2` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `connected_resource_3` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `connected_resource_4` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `connected_resource_5` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `connected_resource_6` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `connected_resource_7` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `connected_resource_8` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `connected_resource_9` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `connected_resource_10` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `resource_1` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `resource_2` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    PRIMARY KEY (`id`),
    UNIQUE KEY `ix_audit_nation_nation_id_change_timestamp` (`nation_id`,`change_timestamp`),
    KEY `ix_audit_nation_nation_id` (`nation_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `audit_alliance` (
    `id` int NOT NULL AUTO_INCREMENT,
    `change_timestamp` datetime NOT NULL,
    `change_code` char(1) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'U=Update, D=Delete',
    `alliance_id` int NOT NULL,
    `name` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
    `updated` datetime DEFAULT NULL,
    `total_nations` int NOT NULL,
    `active_nations` int NOT NULL,
    `percent_active` int NOT NULL,
    `strength` int NOT NULL,
    `average_strength` int NOT NULL,
    `score` float NOT NULL,
    `land` int NOT NULL,
    `infrastructure` int NOT NULL,
    `technology` int NOT NULL,
    `war` int NOT NULL,
    `peace` int NOT NULL,
    `soldiers` int NOT NULL,
    `tanks` int NOT NULL,
    `cruise` int NOT NULL,
    `nukes` int NOT NULL,
    `aircraft` int NOT NULL,
    `navy` int NOT NULL,
    `anarchy` int NOT NULL,
    PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `audit_aid` (
    `id` int NOT NULL AUTO_INCREMENT,
    `change_timestamp` datetime NOT NULL,
    `change_code` char(1) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'U=Update, D=Delete',
    `aid_id` int NOT NULL,
    `sending_nation_id` int NOT NULL,
    `sending_alliance_id` int NOT NULL,
    `sending_team` int NOT NULL,
    `receiving_nation_id` int NOT NULL,
    `receiving_alliance_id` int NOT NULL,
    `receiving_team` int NOT NULL,
    `status` int NOT NULL,
    `money` int NOT NULL,
    `technology` int NOT NULL,
    `soldiers` int NOT NULL,
    `date` datetime DEFAULT NULL,
    `reason` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `audit_war` (
    `id` int NOT NULL AUTO_INCREMENT,
    `change_timestamp` datetime NOT NULL,
    `change_code` char(1) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'U=Update, D=Delete',
    `war_id` int NOT NULL,
    `attacking_nation_id` int NOT NULL,
    `attacking_alliance_id` int NOT NULL,
    `attacking_team` int NOT NULL,
    `defending_nation_id` int NOT NULL,
    `defending_alliance_id` int NOT NULL,
    `defending_team` int NOT NULL,
    `war_status` int NOT NULL,
    `begin_date` datetime DEFAULT NULL,
    `end_date` datetime DEFAULT NULL,
    `reason` varchar(25) COLLATE utf8mb4_unicode_ci NOT NULL,
    `destruction` float NOT NULL,
    `attack_percent` int NOT NULL,
    `defend_percent` int NOT NULL,
    PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =============================================================================
-- DATABASE: vep_db
-- Purpose: Application-specific data (accounts, transactions, lists, roles)
-- =============================================================================

CREATE DATABASE IF NOT EXISTS `vep_db`
    DEFAULT CHARACTER SET latin1
    DEFAULT ENCRYPTION='N';

USE `vep_db`;

-- -----------------------------------------------------------------------------
-- TABLE: account
-- Purpose: User accounts linked to nations
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `account` (
    `id` int NOT NULL AUTO_INCREMENT,
    `nation_id` int NOT NULL,
    `discord` varchar(50) DEFAULT NULL,
    `discord_id` bigint NOT NULL,
    `psw` varchar(5) NOT NULL COMMENT 'Password/access code',
    `va` char(1) NOT NULL COMMENT 'VA type',
    `fm` tinyint(1) NOT NULL COMMENT 'Foreign Minister flag',
    `fac` tinyint(1) NOT NULL COMMENT 'FAC flag',
    `dra` tinyint(1) NOT NULL COMMENT 'DRA flag',
    `date_added` datetime DEFAULT NULL,
    PRIMARY KEY (`id`),
    UNIQUE KEY `nation_id` (`nation_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;

-- -----------------------------------------------------------------------------
-- TABLE: account_removed
-- Purpose: Archive of removed accounts
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `account_removed` (
    `id` int NOT NULL AUTO_INCREMENT,
    `nation_id` int NOT NULL,
    `discord` varchar(50) DEFAULT NULL,
    `discord_id` bigint NOT NULL,
    `psw` varchar(5) NOT NULL,
    `va` char(1) NOT NULL,
    `fm` tinyint(1) NOT NULL,
    `fac` tinyint(1) NOT NULL,
    `dra` tinyint(1) NOT NULL,
    `removed_on` datetime NOT NULL,
    PRIMARY KEY (`id`),
    UNIQUE KEY `nation_id` (`nation_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;

-- -----------------------------------------------------------------------------
-- TABLE: role
-- Purpose: User roles/permissions
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `role` (
    `id` int NOT NULL AUTO_INCREMENT,
    `name` varchar(50) NOT NULL,
    PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- -----------------------------------------------------------------------------
-- TABLE: account_role
-- Purpose: Many-to-many relationship between accounts and roles
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `account_role` (
    `account_id` int NOT NULL,
    `role_id` int NOT NULL,
    UNIQUE KEY `uq_account_role` (`account_id`,`role_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- -----------------------------------------------------------------------------
-- TABLE: transaction
-- Purpose: Tech/cash credit transactions between nations
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `transaction` (
    `id` int NOT NULL AUTO_INCREMENT,
    `aid_id` int DEFAULT NULL,
    `sending_nation_id` int DEFAULT NULL,
    `receiving_nation_id` int DEFAULT NULL,
    `reason_override` varchar(500) DEFAULT NULL,
    `lu` char(2) NOT NULL COMMENT 'Lookup code combining sender/receiver VA types',
    `co` int NOT NULL COMMENT 'Classification',
    `rate` int NOT NULL COMMENT 'Exchange rate',
    `ct` int NOT NULL COMMENT 'Cash transferred for tech credit',
    `cc` int NOT NULL COMMENT 'Cash credit',
    `tc` int NOT NULL COMMENT 'Tech credit',
    `tt` int NOT NULL COMMENT 'Tech transferred for tech credit',
    `created_on` datetime DEFAULT NULL,
    PRIMARY KEY (`id`),
    KEY `aid_id` (`aid_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;

-- -----------------------------------------------------------------------------
-- TABLE: list
-- Purpose: Message distribution lists
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `list` (
    `id` int NOT NULL AUTO_INCREMENT,
    `name` varchar(50) NOT NULL,
    `subject` varchar(40) NOT NULL,
    `message` text,
    `recipients_last_updated_on` datetime DEFAULT NULL,
    `messages_sent_by` int DEFAULT NULL,
    `messages_sent_on` datetime DEFAULT NULL,
    PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- -----------------------------------------------------------------------------
-- TABLE: list_recipient
-- Purpose: Recipients for each distribution list with priority
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `list_recipient` (
    `id` int NOT NULL AUTO_INCREMENT,
    `list_id` int NOT NULL,
    `account_id` int NOT NULL,
    `priority` int NOT NULL COMMENT 'Priority ranking for sending order',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uq_list_recipient` (`list_id`,`account_id`,`priority`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- -----------------------------------------------------------------------------
-- TABLE: account_list_performance
-- Purpose: Track how long accounts stay on each list (for priority calculation)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `account_list_performance` (
    `id` int NOT NULL AUTO_INCREMENT,
    `account_id` int NOT NULL,
    `list_id` int NOT NULL,
    `day_started` date NOT NULL,
    `days_until_removed` int NOT NULL DEFAULT '0',
    PRIMARY KEY (`id`),
    UNIQUE KEY `UNIQUE` (`account_id`,`list_id`,`day_started`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;
