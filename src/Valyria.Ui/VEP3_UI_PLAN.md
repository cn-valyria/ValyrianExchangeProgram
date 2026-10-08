# VEP 3.0 UI Plan: Ledger → Deals

VEP 3.0 (migrations V5–V8) replaces the balance ledger with **slot-based deals**. Each account has
4–6 slots (4, +1 FM, +1 DRA). Each slot has cycle values (tech/cash sent/received per 10-day period).
Two free slots with inverse cycles are matched into a **deal**. Aid between the two partners is linked to
the deal as `deal_activity`. Deals with no activity get flagged (`deal_flag`) and are auto-cancelled
after 5 days, and accounts with open slots wait in `wait_queue` until they're matched.

The UI keeps its layout, navigation and component library (Nuxt UI 3). It drops every credit/debt/
balance/list/activity-score concept and replaces each one with its deal or slot equivalent.

Legend: ✅ done · 🔨 in progress · ⬜ not started · 🔧 needs backend work first

---

## 1. Account page: `pages/account/[id].vue` ✅

| Old | New |
|---|---|
| `Balance.vue` (credit/debt) | `SlotUsage.vue`: X / Y slots in deals, meter, Full / Open / Hold badge |
| `ActivityScore.vue` (avg days to clear debt) | `DealHealth.vue`: number of deals needing action, plus open flags |
| `AidList.vue` (role + balance → send/accept list) | `ActiveDeals.vue`: one row per deal with partner, Discord, *you send / you receive per 10 days* (from my slot's cycles), last activity, status. "Send" deals link to the CN aid form. |
| `CreditHistory.vue` (ledger transactions) | `DealHistory.vue`: paged list of all deals (active + cancelled); expanding a row shows its aid activity |
| `ListPerformance.vue` | removed |

Backend changes made for this page (🔧, done):
- `DealDetail`: added `CreatedOn` and `PartnerDiscord`; `DaysSinceActivity` (and so `NeedsAction`) counts from the deal's creation date when there's no activity yet. Before, a new deal came back as "0 days" forever.
- Readable notification messages instead of raw `1_day_inactive`.
- New `GET /api/deals/account/{accountId}/history?limit&offset` and `GET /api/deals/{dealId}/activity?accountId=`.
- Ownership checks on the account-scoped deal endpoints (caller must own the account or be Admin).

Store: `stores/account.ts` now holds `account`, `dealsSummary`, `dealHistory` and per-deal activity. Ledger types and getters are removed.

## 2. Public pages

| Page | Today | Change |
|---|---|---|
| `pages/index.vue` | Hero copy talks about balances/debts; "Transactions" feature card | Rewrite copy around deals and slots ("get matched with a partner, exchange aid every 10 days"). ⬜ |
| `pages/how-it-works.vue` | Explains balance, credit/debt, aid list; screenshots of old account page | Rewrite sections: slots → matching → deal → keeping a deal active (10-day cycle, 1-day warning, 5-day cancellation). Replace `howitworks-account.png` / `howitworks-aidlist.png` with new screenshots. ⬜ |
| `pages/leaderboard.vue` + `components/leaderboard/*` | Ranked by Activity Score (avg days to clear debt) via `/api/accounts/topScorers` | 🔧 Needs a deal-based metric (e.g. on-time rate or average days between deal activity, deals completed without flags). **Decision needed:** metric definition. Until then, hide the page or keep it behind a flag. ⬜ |
| `components/NavBar.vue` | Admin links to Transactions and Lists | Update the admin menu to match section 3. ⬜ |

## 3. Admin

| Page / component | Today | Change |
|---|---|---|
| `pages/admin/lists.vue`, `stores/adminLists.ts` | Aid lists 1–4 with credit/debt | **Replace** with `pages/admin/deals.vue`: all active deals (search by nation), cancel deal (`PUT /deals/{id}/cancel`), create a manual deal from two free slots (`GET /slots/available/{id}`, `POST /deals`). 🔧 needs a "list all deals" endpoint. ⬜ |
| *(new)* `pages/admin/queue.vue` (or a tab on deals) | — | Wait queue (`GET /deals/admin/wait-queue`): slots needed, waiting since, priority. ⬜ |
| *(new)* flags view (tab on deals) | — | `GET /deals/admin/flagged` + resolve with notes (`PUT /deals/admin/flags/{id}/resolve`). ⬜ |
| `pages/admin/accounts.vue`, `stores/adminAccounts.ts` | Credit/Debt columns, list order | Replace with slots used/total and active deal count. 🔧 `GET /api/accounts` needs those fields (from `account_deal_summary`). ⬜ |
| `components/admin/AccountEditDialog.vue` | Balance/credit/debt section, list info | Replace with a **Slots** section: the account's slots (`GET /slots/account/{id}`), editable cycle values (`PUT /slots/{id}`), and which slot is in which deal. ⬜ |
| `pages/admin/transactions.vue`, `TransactionsFilter.vue`, `ManualTransactionDialog.vue`, `AidBasedTransactionDialog.vue`, `stores/adminTransactions.ts` | Ledger transactions + manual credit/debt adjustments | **Decision needed:** manual credit/debt adjustments make no sense anymore. Either retire the page, or turn it into a read-only **Aid Activity** search (deal_activity + aid) without "Credit Change" columns. ⬜ |
| `pages/admin/index.vue` + `components/admin/dashboard/*` | Credit-based tiles | Remove `PendingCreditTile`, `UnusedCreditStatCard`, `FreeSlotsByList`. Repurpose `ProgramSlotUsageStatCard` (slots in deals / total slots), `SlotRatiosStatCard`, `GeneralProgramStatsCard` (accounts, active deals). Add active deals, queue length and open flags. 🔧 `AdminController` dashboard queries still read the old `balance` / list tables, so they need deal-based versions. ⬜ |
| `pages/admin/settings.vue` | Admin access | No change expected. ✅ |

## 4. Shared cleanup (after sections 2–3 land)

- Delete now-unused: `components/account/{Balance,ActivityScore,AidList,CreditHistory,ListPerformance}.vue` (done in step 1), `stores/shared/{listPerformance,transaction,transactionsPage}.ts` if nothing references them, `infrastructure/constants.ts` → `ADJUSTMENT_TYPES` / `TRANSACTION_TYPES`.
- Backend: retire `/api/accounts/{id}/aidList`, `/listPerformance`, `/transactions`, `ListsController`, and the credit-based admin dashboard endpoints once no UI uses them.
- Update `REDESIGN_STATUS.md` / README screenshots.

## 5. Backend issues spotted along the way (not UI, but they affect what the UI shows)

1. **`check_deal_inactivity` flags any deal with no activity in the last 1 day.** With a 10-day cycle, almost every healthy deal gets a `1_day_inactive` flag daily, and any deal with no aid in 5 days is cancelled. That doesn't fit the "per 10-day period" cycle model in V7. The thresholds in `DealsRepository.GetAccountDealsAsync` (`NeedsAction` at 5 days) have the same issue. The account page shows whatever the backend reports, so this should be settled before launch.
2. `check_deal_inactivity` always records `account_1` as the "flagged" (offender) party, based on slot order rather than on who failed to send. The UI therefore avoids blaming either side in flag messages.
3. **Admin deal/slot endpoints probably can't be reached.** `DealsController` admin actions and all of `SlotsController` use `[Authorize(Roles = "Admin")]`, but the roles the UI knows about are `Super Admin`, `Admin - Lists` and `Admin (Read-only)`. Unless a role literally named `Admin` exists in the DB, these return 403 for everyone. Fix this before section 3 starts. The new account-scoped checks use `Super Admin` / `Admin (Read-only)`.
4. `AccountsController` endpoints (`GET /api/accounts/{id}` etc.) don't verify the caller owns the account. Only the UI checks this. The deal endpoints now do.
