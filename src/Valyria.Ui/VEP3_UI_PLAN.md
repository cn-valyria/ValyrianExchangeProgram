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
| `components/NavBar.vue` | Admin links to Transactions and Lists | Admin menu now links to Aid Activity and Deals. ✅ |

## 3. Admin ✅

| Page / component | Was | Now |
|---|---|---|
| `pages/admin/deals.vue` (replaces `lists.vue`) | Aid lists 1–4 with credit/debt | Three tabs. **Active Deals**: paged and searchable, with both parties, what each sends, last aid, days idle and status; cancel with a reason; create a manual deal from two open slots (warns if the cycles aren't exact inverses). **Wait Queue**: priority order. **Flags**: resolve with notes. Same role access the Lists page had; read-only admins get no actions. |
| `pages/admin/aid-activity.vue` (replaces `transactions.vue`) | Ledger transactions + manual credit/debt adjustments | Read-only search of all aid sent or received by VEP accounts. On Hold accounts are excluded by default, with a toggle to include them. A "Deal #n" or "No deal" badge on each row, plus an "only aid with no deal" toggle. The filter slideover covers name search (sent/received/either) and date range. |
| `pages/admin/accounts.vue` | Credit/debt and list-activity columns | "Slots in Deals" (used / total) and "Open Flags" columns. |
| `components/admin/AccountEditDialog.vue` | Balance/credit/debt section | **Slots** section (`AccountSlotsEditor.vue`): each slot's cycle values, editable and saved per slot, with an in-a-deal / open badge. |
| `pages/admin/index.vue` + `components/admin/dashboard/*` | Credit-based tiles | Stat cards: accounts / active deals, slots in deals, deal health (accounts waiting + open flags), incoming/outgoing slot ratios. Tiles: accounts by role, slots by aid direction per alliance, deal aid per day, accounts by alliance, open slots by role. Removed: pending credit, unused credit, free slots by list. |
| `pages/admin/settings.vue` | Admin access | No change. |

Backend changes for this section:
- `GET /api/deals/admin/active` (paged, searchable) and `GET /api/deals/admin/aid-activity` (filters: search, direction, sentSince/Until, includeOnHold, unmatchedOnly).
- The flagged-deals query now maps `flag_type` to the enum in SQL. Before, `'1_day_inactive'` couldn't be parsed into `DealFlagType`, so the endpoint threw whenever a flag existed.
- `AccountDetail` gained `SlotsUsed` / `SlotsTotal` / `OpenFlagCount`. The shared account query no longer joins `balance`, `aid_activity`, `account_list_performance` or the aid totals, and the alliance join is now a left join.
- The dashboard queries were rewritten against `slot`, `deal`, `deal_activity`, `deal_flag`, `wait_queue` and `account_deal_summary`. The `unused-credit-summary`, `pending-credit-per-alliance`, `transactions-per-day` and `free-slots-per-list` endpoints were replaced by `deal-health-summary`, `deal-aid-per-day` and `open-slots-per-role`.
- **Role-based authorization was removed from `DealsController` and `SlotsController` for now** (sign-in is still required). The UI's role gating is the only access control on admin actions until auth is revisited.

## 4. Shared cleanup

- ✅ Deleted the legacy account components, the Lists/Transactions pages and their stores, the transaction dialogs and filter, `stores/shared/{listPerformance,transaction,transactionsPage}.ts`, `TRANSACTION_TYPES` / `ADJUSTMENT_TYPES`, and `transactionClassifications`.
- ⬜ Backend: nothing in the UI calls `ListsController`, `TransactionsController`, `/api/accounts/{id}/aidList`, `/listPerformance` or `/transactions` anymore, so they can be retired. `/api/accounts/topScorers` is still used by the leaderboard (section 2).
- ⬜ Update `REDESIGN_STATUS.md` / README screenshots.

## 5. Backend issues spotted along the way (not UI, but they affect what the UI shows)

1. **`check_deal_inactivity` flags any deal with no activity in the last 1 day.** With a 10-day cycle, almost every healthy deal gets a `1_day_inactive` flag daily, and any deal with no aid in 5 days is cancelled. That doesn't fit the "per 10-day period" cycle model in V7. The thresholds in `DealsRepository.GetAccountDealsAsync` (`NeedsAction` at 5 days) have the same issue. The account page shows whatever the backend reports, so this should be settled before launch.
2. `check_deal_inactivity` always records `account_1` as the "flagged" (offender) party, based on slot order rather than on who failed to send. The UI therefore avoids blaming either side in flag messages.
3. **Authorization (to-do):** the `[Authorize(Roles = "Admin")]` checks referenced a role that no longer exists. They've been removed, along with the account-ownership check on the account-scoped deal endpoints. No endpoint verifies roles or account ownership right now; only the UI does. Revisit once it's confirmed whether role claims are registered correctly.
4. `CreatedBy` / `CancelledBy` / `ResolvedBy` / slot `UpdatedBy` come from `User.Identity.Name`. The JWT has no name claim, so these will likely all record `"unknown"`. Same root cause as item 3.
5. The aid-activity search left-joins `account` twice over the whole `aid` table, with an OR across both sides. Fine at current volumes, but worth an index check if it gets slow.
