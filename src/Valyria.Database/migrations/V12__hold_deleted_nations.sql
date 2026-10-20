-- =============================================================================
-- V12__hold_deleted_nations.sql
-- VEP 3.0: Put accounts on hold when their nation is deleted
--
-- CN deletes inactive nations, and update_nation_data drops them from
-- cybernations_db.nation. Their VEP accounts stayed active, so they kept
-- their deals and were matched into new ones that could never be completed.
--
-- The data loader now calls hold_deleted_nation_accounts before matching.
-- It cancels the account's active deals and moves it to the 'H' (hold) role.
-- The role change fires after_account_update_regenerate_slots, which resets
-- the account's slots to the hold defaults (never matched). If the nation
-- comes back, an admin can change the role back as usual.
--
-- Accounts can only be created for nations in cybernations_db.nation (see
-- AccountsRepository.FindProspectAccountAsync), so an account without a
-- nation means the nation was deleted.
--
-- Changes:
-- 1. New procedure: hold_deleted_nation_accounts
-- =============================================================================

USE `vep_db`;

DELIMITER $$

-- =============================================================================
-- PROCEDURE: hold_deleted_nation_accounts
-- Purpose: Cancel the deals of accounts whose nation no longer exists in CN,
--          and put those accounts on hold
--
-- Parameters:
--   OUT p_accounts_held - Number of accounts put on hold
--
-- Does nothing if cybernations_db.nation is empty, so a failed nation import
-- can't put every account on hold and cancel every deal
-- =============================================================================
DROP PROCEDURE IF EXISTS `hold_deleted_nation_accounts`$$

CREATE PROCEDURE `hold_deleted_nation_accounts`(
    OUT p_accounts_held INT
)
MODIFIES SQL DATA
BEGIN
    SET p_accounts_held = 0;

    IF EXISTS (SELECT 1 FROM cybernations_db.nation) THEN
        -- Cancel first so the deals get a clear reason. Changing the role
        -- would otherwise cancel them as 'Account role changed'
        UPDATE deal d
        JOIN slot s ON s.id IN (d.slot_1_id, d.slot_2_id)
        JOIN account a ON s.account_id = a.id
        LEFT JOIN cybernations_db.nation n ON a.nation_id = n.id
        SET
            d.status = 'cancelled',
            d.cancelled_on = CURRENT_TIMESTAMP,
            d.cancelled_by = 'system',
            d.cancellation_reason = 'Nation deleted'
        WHERE n.id IS NULL
            AND a.va <> 'H'
            AND d.status = 'active';

        UPDATE account a
        LEFT JOIN cybernations_db.nation n ON a.nation_id = n.id
        SET a.va = 'H'
        WHERE n.id IS NULL
            AND a.va <> 'H';

        SET p_accounts_held = ROW_COUNT();
    END IF;
END$$

DELIMITER ;

-- =============================================================================
-- End of V12 Migration
-- =============================================================================
