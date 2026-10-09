-- =============================================================================
-- V11__mass_messages.sql
-- VEP 3.0: Daily mass messages
--
-- Brings back the admin Lists page from VEP 2.5 as the Messages page. Admins
-- (and "Admin - Lists" volunteers) send a CN mass message each day to every
-- account that needs to do something for its deals. CN has no API for this,
-- so the page builds the recipient batches and the admins track who sent them.
--
-- Messages never contain deal-specific information; they tell the recipient
-- to log in to VEP and check their Action Needed card. The recipients of each
-- message are worked out by the API from deal_state and deal_flag, and each
-- account is sent at most one message a day, the most urgent that applies:
--   final_warning    Flagged, and a deal is cancelled at the next daily check
--   overdue          Flagged for holding up a deal
--   accept_aid       Has an aid offer to accept. An offer that expires cancels
--                    the deal outright, so this beats send_aid
--   send_aid         Has aid to send, or aid slots to free up
--
-- Changes:
-- 1. New table: mass_message (one row per message, with its template)
-- 2. New table: mass_message_sent (which messages were sent on each CN day)
--
-- The VEP 2.5 list tables are left in place, but nothing uses them anymore.
--
-- Dates: CN dates are server UTC shifted to -05:00, as in the aid_activity view
-- =============================================================================

USE `vep_db`;

-- =============================================================================
-- TABLE: mass_message
-- Purpose: A daily mass message and the template admins copy into CN
--
-- code is what the API uses to pick the message's recipients. In subject and
-- message, {{date}} becomes today's date and {{siteUrl}} the VEP site's
-- address. CN limits subjects to 40 characters.
-- =============================================================================
CREATE TABLE IF NOT EXISTS `mass_message` (
    `id` INT AUTO_INCREMENT PRIMARY KEY,
    `code` ENUM('final_warning', 'overdue', 'accept_aid', 'send_aid') NOT NULL,
    `name` VARCHAR(50) NOT NULL,
    `description` VARCHAR(255) NOT NULL COMMENT 'Who receives this message, shown to admins',
    `subject` VARCHAR(40) NOT NULL,
    `message` TEXT NOT NULL,
    `sort_order` INT NOT NULL,

    UNIQUE KEY `unique_code` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Daily mass messages sent to accounts with deal activity needed';

-- =============================================================================
-- TABLE: mass_message_sent
-- Purpose: Lets admins see which of today's messages someone already sent
--
-- One row per message per CN day, so the "Messages Sent" toggle resets itself
-- when the CN day rolls over.
-- =============================================================================
CREATE TABLE IF NOT EXISTS `mass_message_sent` (
    `id` INT AUTO_INCREMENT PRIMARY KEY,
    `mass_message_id` INT NOT NULL,
    `cn_date` DATE NOT NULL COMMENT 'CN date the messages were sent for',
    `sent_by` INT NOT NULL COMMENT 'Account of the admin who sent them',
    `sent_on` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    FOREIGN KEY (`mass_message_id`) REFERENCES `mass_message`(`id`) ON DELETE CASCADE,
    FOREIGN KEY (`sent_by`) REFERENCES `account`(`id`) ON DELETE CASCADE,

    UNIQUE KEY `unique_message_day` (`mass_message_id`, `cn_date`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Daily record of which mass messages have been sent';

INSERT INTO `mass_message` (`code`, `name`, `description`, `subject`, `message`, `sort_order`)
VALUES
(
    'send_aid',
    'Send Aid',
    'Accounts with aid to send, or aid slots to free up, today',
    'VEP Aid to Send - {{date}}',
    'Hello!

You have aid to send today for your VEP deals, or an aid slot you need to free up so your deals can continue.

Log in to VEP at {{siteUrl}} and check the "Action Needed Today" card on your account page to see exactly what to do. Doing it today keeps your deals from being flagged.

If you have any questions, reach out to VEP management on Discord.

Thank you!
VEP Management',
    1
),
(
    'accept_aid',
    'Accept Aid',
    'Accounts with an aid offer waiting for them to accept',
    'VEP Aid to Accept - {{date}}',
    'Hello!

You have an aid offer from a VEP partner waiting for you to accept in-game. If an offer expires without being accepted, the deal is cancelled and both nations are flagged.

Log in to VEP at {{siteUrl}} and check the "Action Needed Today" card on your account page to see what to accept, along with anything else your deals need today.

If you have any questions, reach out to VEP management on Discord.

Thank you!
VEP Management',
    2
),
(
    'overdue',
    'Overdue',
    'Accounts with a flag for holding up one of their deals',
    'VEP Deal Flagged - {{date}}',
    'Hello,

One or more of your VEP deals has been flagged because it is waiting on you, and it is now overdue.

While any of your deals are flagged, you won''t be matched with new partners. If a deal stays held up for 5 days, it is cancelled and you will need VEP management to review your account before you can be matched again.

Please log in to VEP at {{siteUrl}} as soon as possible and check the "Action Needed Today" card on your account page to see what needs to be done.

If something is keeping you from sending aid, let VEP management know on Discord.

Thank you,
VEP Management',
    3
),
(
    'final_warning',
    'Final Warning',
    'Flagged accounts with a deal that is cancelled at the next daily check',
    'VEP FINAL WARNING - {{date}}',
    'Hello,

This is a final warning: one or more of your VEP deals has been held up by you for 4 days, and will be cancelled at the next daily update unless you act today.

If a deal is cancelled, you won''t be matched with new partners until VEP management reviews your account.

Log in to VEP at {{siteUrl}} now and check the "Action Needed Today" card on your account page to see what needs to be done.

If something is keeping you from sending aid, let VEP management know on Discord right away.

Thank you,
VEP Management',
    4
);

-- =============================================================================
-- End of V11 Migration
-- =============================================================================
