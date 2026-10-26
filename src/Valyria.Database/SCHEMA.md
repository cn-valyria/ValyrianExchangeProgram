# Valyria Database Schema Overview

This document provides a high-level overview of the database schema and migration structure.

## Migration Files

The schema is split into logical migration files for easier maintenance and understanding:

### [V1__initial_schema.sql](migrations/V1__initial_schema.sql) (23KB)
**Tables created:** Core table structure for both databases

**cybernations_db tables:**
- **Staging tables** (daily file imports):
  - `todays_file_nation` - Nation data staging
  - `todays_file_alliance` - Alliance data staging
  - `todays_file_aid` - Aid data staging
  - `todays_file_war` - War data staging

- **Production tables** (current state):
  - `nation` - Current nation data
  - `alliance` - Current alliance data
  - `aid` - Current aid transactions
  - `war` - Current/historical war data

- **Audit tables** (change history):
  - `audit_nation` - Nation change tracking
  - `audit_alliance` - Alliance change tracking
  - `audit_aid` - Aid change tracking
  - `audit_war` - War change tracking

**vep_db tables:**
- `account` - User accounts linked to nations
- `account_removed` - Archive of removed accounts
- `role` - User roles/permissions
- `account_role` - Account-to-role mapping
- `transaction` - Tech/cash credit transactions
- `list` - Message distribution lists
- `list_recipient` - List recipients with priority
- `account_list_performance` - List membership tracking for priority calculation

### [V2__triggers_and_audit.sql](migrations/V2__triggers_and_audit.sql) (12KB)
**Triggers created:** Automatic audit trail for data changes

- `tr_nations_update` / `tr_nations_delete` - Track nation changes
- `tr_alliance_update` / `tr_alliance_delete` - Track alliance changes
- `tr_aid_update` / `tr_aid_delete` - Track aid changes
- `tr_war_update` / `tr_war_delete` - Track war changes

All triggers record changes to their respective audit tables with:
- Change timestamp
- Change type (U=Update, D=Delete)
- Previous values (OLD record)

### [V3__views.sql](migrations/V3__views.sql) (6.3KB)
**Views created:** Aggregated data for reporting and calculations

**cybernations_db views:**
- `sent_txn` - Aggregate sent transactions by nation/VA type
- `recd_txn` - Aggregate received transactions by nation/VA type

**vep_db views:**
- `balance` - Calculate credit/debt balances by account and VA type
- `aid_activity` - Calculate aid slot usage vs. available slots

### [V4__stored_procedures.sql](migrations/V4__stored_procedures.sql) (33KB)
**Stored procedures created:** Business logic and data processing

**cybernations_db procedures:**
- `update_nation_data()` - Process daily nation file imports
- `update_alliance_data()` - Process daily alliance file imports
- `update_aid_data()` - Process daily aid file imports (marks missing active aid as deleted)
- `update_war_data()` - Process daily war file imports (preserves historical data)

**vep_db procedures:**
- `load_todays_transactions()` - Convert aid records to VEP transactions
- `load_todays_lists()` - Regenerate distribution lists with updated priorities
- `get_pending_credit_per_alliance()` - Report pending credit by alliance and age
- `search_transactions()` - Flexible transaction search with multiple filters

### [V5__vep_3_deals.sql](migrations/V5__vep_3_deals.sql)
**VEP 3.0: Deal-Based System Migration**

This migration introduces the deal-based system to replace the balance-ledger approach.

**Tables created:**
- `deal` - Bilateral aid exchange agreements between two accounts
- `deal_activity` - Track aid sent/received for each deal on 10-day schedule
- `deal_flag` - Track inactive or problematic deals requiring admin attention
- `wait_queue` - Priority queue for accounts waiting to be matched with deals

**Views created:**
- `account_deal_summary` - Replaces `balance` view with deal-based slot calculations

**Stored procedures created:**
- `track_deal_activity()` - Link newly imported aid records to existing deals
- `check_deal_inactivity()` - Flag inactive deals and create replacement deals
- `match_deals_daily()` - Daily deal matching to fill empty slots

**Migration Strategy:**
- Hard cutover from balance system to deal system (no transition period)
- All accounts start fresh with zero deals
- Historical balance/credit/debt data can be archived separately
- Replaces `load_todays_lists()` with `match_deals_daily()`

### [V9__deal_state.sql](migrations/V9__deal_state.sql)
**VEP 3.0: Expected-activity tracking for deals**

A deal is a repeating sequence of legs (one CN aid offer each). The cash sender goes first and sends all of its cycles, then the other side sends all of its cycles (Buyer/Seller 1:1 is B, S, B, S...; 1 cash : 2 tech is B, S, S...; Donor/Collector and Farm/Receiver always use the same sender). Aid counts as a leg only if it comes from the expected sender, to the expected receiver, dated on or after the deal was created. Amounts are ignored and out-of-sequence aid is never counted.

Each daily run puts every active deal in one state (`deal_state`):
- `pending_start` - the pair has never been able to exchange aid yet (slots full or old aid between them). Nobody is at fault
- `in_flight` - a counted leg is active between the pair (10 days)
- `awaiting_send` - both nations have a free slot and nothing is active between them. The expected sender is responsible
- `blocked` - activated, but a nation used its slots for non-deal aid (`sender_slots_full`, `receiver_slots_full`), or the wrong side sent aid (`out_of_sequence_aid`). That nation is responsible

**Tables/views:** `deal_state` (new), `deal_sequence` view (new), `deal_activity` rebuilt as one row per leg with outcome tracking (`approved_on`, `failed_on`)

**Procedures:** `track_deal_activity()` (rewritten), `update_deal_state()` (new)

### [V10__deal_state_flags.sql](migrations/V10__deal_state_flags.sql)
**VEP 3.0: Deal flags based on expected activity**

`check_deal_inactivity()` counts the days `deal_state.responsible_account_id` has been responsible:
- 1+ days: `1_day_inactive` warning, auto-resolved once the account stops holding up the deal
- 5+ days: `5_day_replacement`, deal cancelled, admin resolves
- A leg whose offer is cancelled or expires unaccepted: deal cancelled, `aid_failed` flag on both accounts, admin resolves

Any unresolved flag keeps the account out of matching.

### [V11__mass_messages.sql](migrations/V11__mass_messages.sql)
**VEP 3.0: Daily mass messages (replaces the VEP 2.5 lists)**

- `mass_message` - one row per daily CN mass message (`send_aid`, `accept_aid`, `overdue`, `final_warning`) with its subject/message template
- `mass_message_sent` - which messages an admin sent on each CN date (unique per message per day, so the admin page's "Messages Sent" toggle resets daily)

Recipients aren't stored; the API works them out from `deal_state` and `deal_flag` each time the page loads. The old `list`, `list_recipient` and `account_list_performance` tables are left in place but unused.

### [V15__restore_flagged_deals.sql](migrations/V15__restore_flagged_deals.sql)
**VEP 3.0: Restoring deals cancelled by a flag**

Admins can restore a deal cancelled by a `5_day_replacement` or `aid_failed` flag. The deal goes back to active with a fresh `deal_state`, and its cancellation flags are resolved. `deal_activity.failure_excused` (new) marks the failed legs of a restored deal, which `check_deal_inactivity()` (rewritten) no longer cancels the deal over.

## Database Architecture

### Two-Database Design

**cybernations_db** (Core Game Data)
- Stores raw Cybernations game data from daily file imports
- Uses staging tables (`todays_file_*`) for imports
- Production tables updated via stored procedures
- Audit tables track all changes automatically via triggers
- Character set: utf8mb4 (full Unicode support)

**vep_db** (Application Data)
- Stores VEP-specific application data
- Manages user accounts, transactions, and lists
- References cybernations_db for nation/alliance data
- Character set: latin1 (legacy, consider migrating to utf8mb4)

### Data Flow

```
Daily File Import → todays_file_* (staging)
                 ↓
         update_*_data() procedures
                 ↓
         Production tables (nation, alliance, aid, war)
                 ↓
         Triggers → audit_* tables
                 ↓
         Views (sent_txn, recd_txn, balance, aid_activity)
                 ↓
         Application queries
```

### Transaction System Flow (VEP 2.5 - Legacy)

```
Aid created in Cybernations → aid table
                            ↓
              load_todays_transactions()
                            ↓
              transaction table (VEP credit system)
                            ↓
              sent_txn/recd_txn views
                            ↓
              balance view (credit/debt calculation)
                            ↓
              load_todays_lists() (priority recalculation)
```

### Deal System Flow (VEP 3.0 - Current)

```
Aid created in Cybernations → aid table
                            ↓
              track_deal_activity()
                            ↓
              deal_activity table (links aid to deals as legs)
                            ↓
              update_deal_state()
                            ↓
              deal_state table (whose turn, who is responsible)
                            ↓
              check_deal_inactivity()
                            ↓
              deal_flag table (flags inactive deals)
                            ↓
              match_deals_run()
                            ↓
              deal table (active bilateral agreements)
                            ↓
              account_deal_summary view (slot calculations)
```

## Key Concepts

### VA Types
Account VA (Verification Alliance) types determine credit/debt calculation:
- **B, W** - Cash senders (tech buyers)
- **S, N, Q** - Tech senders (tech sellers)
- **C, R** - Special credit types
- **F, D, V, P** - Special debt types
- **H** - Hold (excluded from transactions)

### Aid Status Codes
- `1` - Pending/Active
- `3` - Deleted/Cancelled
- `4` - Manual transaction (no aid_id)

### List Assignment Logic
Accounts are assigned to lists (1-4) based on balance and VA type:
1. Cash needed, tech available
2. Tech needed, cash available
3. Fill orders
4. Cash available, tech needed

Priority within lists is calculated based on:
- `days_until_removed` - Time on current list (longer = higher priority)
- `activity_score` - Average historical performance
- `nation_id` - Tiebreaker

## Indexes

### Critical Performance Indexes

**aid table:**
- `idx_aid_sender_status_date` - Query sender's active aid
- `idx_aid_receiver_status_date` - Query receiver's active aid

**war table:**
- `idx_war_attacker_defender_status_end` - Query specific wars
- `idx_war_attacker_status_end` - Query attacker's wars
- `idx_war_defender_status_end` - Query defender's wars
- `idx_war_attacker_begin` - Query wars by start date
- `idx_war_defender_begin` - Query wars by start date

**audit_nation table:**
- `ix_audit_nation_nation_id_change_timestamp` - Unique constraint + query optimization
- `ix_audit_nation_nation_id` - Quick nation history lookup

## Data Retention

- **audit_nation** - Full history, auto-incremented to 1,168,382 records
- **audit_alliance** - Full history, auto-incremented to 116,393 records
- **audit_aid** - Full history, auto-incremented to 501,217 records
- **audit_war** - Full history, auto-incremented to 195,645 records
- **aid** - Active aid only (9+ days handled by update_aid_data)
- **war** - Full historical data (never deleted)

## Migration Best Practices

When adding new migrations:

1. **Use semantic versioning**: V5, V6, etc.
2. **One logical change per migration**:
   - V5 - Add new feature tables
   - V6 - Add indexes for V5 tables
   - V7 - Add stored procedures for V5 feature
3. **Always use `IF NOT EXISTS` / `IF EXISTS`**
4. **Test on a copy of production data first**
5. **Document purpose in migration file header**

## Future Considerations

### Potential Improvements

1. **Normalize connected_resource columns**
   - Currently 10 separate columns in nation table
   - Consider separate `nation_resource` table

2. **Migrate vep_db to utf8mb4**
   - Currently using latin1 charset
   - Would support full Unicode in account data

3. **Add foreign key constraints**
   - Currently no FKs between tables
   - Would enforce referential integrity

4. **Partition audit tables by date**
   - Large audit tables could benefit from partitioning
   - Faster queries for recent history

5. **Add computed columns**
   - Could compute `slots_used` directly on account table
   - Would simplify `aid_activity` view logic

## Troubleshooting

### Common Issues

**Checksum mismatch on V1__initial_schema.sql**
- Migration was modified after being applied
- Fix: `flyway repair` or create a new migration with changes

**Triggers not firing**
- Check DEFINER permissions (`base_admin@%`)
- Verify triggers exist: `SHOW TRIGGERS FROM cybernations_db;`

**View returns incorrect balances**
- Check `sent_txn` / `recd_txn` views are up-to-date
- Verify VA type logic in `balance` view matches business rules

**Stored procedure fails**
- Check staging tables have data before running update procedures
- Verify cross-database references work (cybernations_db.aid from vep_db)

## Schema Maintenance

### Daily Operations

**VEP 3.0 (Current):**
1. Import daily files to `todays_file_*` tables
2. Run update procedures: `CALL update_nation_data();` etc.
3. Track deal activity: `CALL track_deal_activity();`
4. Check inactive deals: `CALL check_deal_inactivity();`
5. Match new deals: `CALL match_deals_run(@deals_created);`

The data loader runs steps 3-5 on every run (`DataLoaderRepository.ProcessDealsAsync`).

**VEP 2.5 (Legacy - deprecated):**
1. Import daily files to `todays_file_*` tables
2. Run update procedures: `CALL update_nation_data();` etc.
3. Process transactions: `CALL load_todays_transactions();`
4. Update lists: `CALL load_todays_lists();` ⚠️ Replaced by `match_deals_run()`

### Weekly/Monthly
- Review audit table growth
- Optimize slow queries
- Backup before major changes

### Before Deployment
- Test migrations on production copy
- Review `flyway_schema_history` for conflicts
- Plan rollback strategy
