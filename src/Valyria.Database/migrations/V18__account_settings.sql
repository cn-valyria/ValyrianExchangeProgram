-- =============================================================================
-- V18__account_settings.sql
-- VEP 3.0: Account settings members can change themselves
--
-- Members can turn off the Discord pings the data loader sends (new deals and
-- the daily mass messages) from the settings on their account page. The mass
-- messages still list them for admins, since those go out in CN.
--
-- Changes:
-- 1. account: discord_notifications_enabled
-- =============================================================================

USE `vep_db`;

ALTER TABLE `account`
    ADD COLUMN `discord_notifications_enabled` BOOLEAN NOT NULL DEFAULT TRUE
        COMMENT 'False if the member opted out of the data loader''s Discord pings'
        AFTER `discord_id`;
