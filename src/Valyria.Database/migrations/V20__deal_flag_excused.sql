-- =====================================================
-- VEP 3.0 Leaderboard: excused deal flags
-- =====================================================
-- The leaderboard ranks accounts by unexcused flags per outgoing leg over the last 90 CN days. Some flags aren't
-- the flagged account's fault: VEP flags both accounts when an aid offer fails, and some flags were raised under
-- rules VEP later corrected. Admins excuse these when resolving the flag, and excused flags don't count.
-- =====================================================

USE `vep_db`;

ALTER TABLE `deal_flag`
    ADD COLUMN `excused` BOOLEAN NOT NULL DEFAULT FALSE
        COMMENT 'TRUE when the flagged account was not at fault, so the flag does not count against them'
        AFTER `admin_notes`,
    ADD INDEX `idx_flagged_account_on` (`flagged_account_id`, `flagged_on`);

-- Flags that V9 and V13 resolved because they were raised under rules VEP later corrected
UPDATE `deal_flag`
SET `excused` = TRUE
WHERE `resolved_by` = 'system'
  AND (`admin_notes` LIKE 'Resolved by V9:%' OR `admin_notes` LIKE 'Resolved by V13:%');

-- Failed aid offers that an admin restored the deal for, which already excused the failed leg
UPDATE `deal_flag`
SET `excused` = TRUE
WHERE `flag_type` = 'aid_failed'
  AND `admin_notes` LIKE 'Deal restored%';
