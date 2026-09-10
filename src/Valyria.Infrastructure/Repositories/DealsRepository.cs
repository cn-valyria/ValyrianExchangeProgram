using Valyria.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using MySql.Data.MySqlClient;
using Valyria.Core.DTOs;
using Valyria.Core.Entities;

namespace Valyria.Infrastructure.Repositories;

public class DealsRepository : IDealsRepository
{
    private readonly string _connectionString;

    public DealsRepository(string connectionString) => _connectionString = connectionString;

    public async Task<AccountDealsSummary> GetAccountDealsAsync(int accountId)
    {
        using var connection = new MySqlConnection(_connectionString);

        // Get slot summary
        var summary = await connection.QueryFirstOrDefaultAsync<AccountDealsSummary>(@"
            SELECT
                slots_used AS SlotsUsed,
                slots_total AS SlotsTotal,
                slots_available AS SlotsAvailable,
                slots_full AS SlotsFull
            FROM account_deal_summary
            WHERE account_id = @accountId",
            new { accountId });

        if (summary == null)
        {
            return new AccountDealsSummary
            {
                SlotsUsed = 0,
                SlotsTotal = 4,
                SlotsAvailable = 4,
                SlotsFull = false,
                ActiveDeals = new List<DealDetail>(),
                Notifications = new List<DealNotification>()
            };
        }

        // Get active deals with partner info
        var deals = await connection.QueryAsync<DealDetail, AccountSimplified, DealDetail>(@"
            SELECT
                d.id AS Id,
                d.direction AS Direction,
                CASE
                    WHEN d.direction = 'bidirectional' THEN 'Both'
                    WHEN d.account_1_id = @accountId THEN 'Sender'
                    ELSE 'Receiver'
                END AS MyRole,
                da.activity_date AS LastActivityDate,
                DATEDIFF(CURDATE(), da.activity_date) AS DaysSinceActivity,
                CASE
                    WHEN DATEDIFF(CURDATE(), da.activity_date) >= 5 THEN TRUE
                    ELSE FALSE
                END AS NeedsAction,
                CASE
                    WHEN DATEDIFF(CURDATE(), da.activity_date) >= 5 THEN 'Send aid to maintain deal'
                    WHEN DATEDIFF(CURDATE(), da.activity_date) >= 1 THEN 'Deal may become inactive soon'
                    ELSE NULL
                END AS ActionNeeded,
                -- Partner account info
                CASE WHEN d.account_1_id = @accountId THEN a2.id ELSE a1.id END AS AccountId,
                CASE WHEN d.account_1_id = @accountId THEN n2.id ELSE n1.id END AS NationId,
                CASE WHEN d.account_1_id = @accountId THEN n2.nation_name ELSE n1.nation_name END AS NationName,
                CASE WHEN d.account_1_id = @accountId THEN n2.ruler_name ELSE n1.ruler_name END AS RulerName,
                CASE WHEN d.account_1_id = @accountId THEN al2.name ELSE al1.name END AS AllianceName
            FROM deal d
            LEFT JOIN account a1 ON d.account_1_id = a1.id
            LEFT JOIN account a2 ON d.account_2_id = a2.id
            LEFT JOIN cybernations_db.nation n1 ON a1.nation_id = n1.id
            LEFT JOIN cybernations_db.nation n2 ON a2.nation_id = n2.id
            LEFT JOIN cybernations_db.alliance al1 ON n1.alliance_id = al1.id
            LEFT JOIN cybernations_db.alliance al2 ON n2.alliance_id = al2.id
            LEFT JOIN (
                SELECT deal_id, MAX(activity_date) AS activity_date
                FROM deal_activity
                GROUP BY deal_id
            ) da ON d.id = da.deal_id
            WHERE (d.account_1_id = @accountId OR d.account_2_id = @accountId)
                AND d.status = 'active'
            ORDER BY DaysSinceActivity DESC",
            (deal, partner) =>
            {
                deal.Partner = partner;
                return deal;
            },
            new { accountId },
            splitOn: "AccountId");

        summary.ActiveDeals = deals.ToList();

        // Get notifications for flagged deals
        var notifications = await connection.QueryAsync<DealNotification>(@"
            SELECT
                CASE
                    WHEN df.flag_type = '5_day_replacement' THEN 'error'
                    WHEN df.flag_type = '1_day_inactive' THEN 'warning'
                    ELSE 'info'
                END AS Type,
                CONCAT('Deal with ', n.nation_name, ' has been flagged: ', df.flag_type) AS Message,
                df.deal_id AS DealId
            FROM deal_flag df
            JOIN deal d ON df.deal_id = d.id
            JOIN account partner_acct ON
                CASE WHEN df.flagged_account_id = @accountId
                    THEN df.innocent_account_id
                    ELSE df.flagged_account_id
                END = partner_acct.id
            JOIN cybernations_db.nation n ON partner_acct.nation_id = n.id
            WHERE (df.flagged_account_id = @accountId OR df.innocent_account_id = @accountId)
                AND df.resolved_on IS NULL
                AND d.status = 'active'",
            new { accountId });

        summary.Notifications = notifications.ToList();

        return summary;
    }

    public async Task<Deal> GetDealByIdAsync(int dealId)
    {
        using var connection = new MySqlConnection(_connectionString);

        var deal = await connection.QueryAsync<Deal, AccountSimplified, AccountSimplified, Deal>(@"
            SELECT
                d.id AS Id,
                d.account_1_id AS Account1Id,
                d.account_2_id AS Account2Id,
                d.direction AS Direction,
                d.status AS Status,
                d.created_on AS CreatedOn,
                d.created_by AS CreatedBy,
                d.cancelled_on AS CancelledOn,
                d.cancelled_by AS CancelledBy,
                d.cancellation_reason AS CancellationReason,
                -- Account 1
                a1.id AS AccountId,
                n1.id AS NationId,
                n1.nation_name AS NationName,
                n1.ruler_name AS RulerName,
                al1.name AS AllianceName,
                -- Account 2
                a2.id AS AccountId,
                n2.id AS NationId,
                n2.nation_name AS NationName,
                n2.ruler_name AS RulerName,
                al2.name AS AllianceName
            FROM deal d
            JOIN account a1 ON d.account_1_id = a1.id
            JOIN account a2 ON d.account_2_id = a2.id
            JOIN cybernations_db.nation n1 ON a1.nation_id = n1.id
            JOIN cybernations_db.nation n2 ON a2.nation_id = n2.id
            LEFT JOIN cybernations_db.alliance al1 ON n1.alliance_id = al1.id
            LEFT JOIN cybernations_db.alliance al2 ON n2.alliance_id = al2.id
            WHERE d.id = @dealId",
            (deal, account1, account2) =>
            {
                deal.Account1 = account1;
                deal.Account2 = account2;
                return deal;
            },
            new { dealId },
            splitOn: "AccountId,AccountId");

        return deal.FirstOrDefault();
    }

    public async Task<int> CreateDealAsync(CreateDealRequest request)
    {
        using var connection = new MySqlConnection(_connectionString);

        // Ensure account_1_id < account_2_id for constraint compatibility
        var account1 = Math.Min(request.Account1Id, request.Account2Id);
        var account2 = Math.Max(request.Account1Id, request.Account2Id);

        var dealId = await connection.QuerySingleAsync<int>(@"
            INSERT INTO deal (account_1_id, account_2_id, direction, created_by)
            VALUES (@account1, @account2, @direction, @createdBy);
            SELECT LAST_INSERT_ID();",
            new
            {
                account1,
                account2,
                direction = request.Direction.ToString().ToLower(),
                createdBy = request.CreatedBy
            });

        return dealId;
    }

    public async Task CancelDealAsync(int dealId, string reason, string cancelledBy)
    {
        using var connection = new MySqlConnection(_connectionString);

        await connection.ExecuteAsync(@"
            UPDATE deal
            SET status = 'cancelled',
                cancelled_on = CURRENT_TIMESTAMP,
                cancelled_by = @cancelledBy,
                cancellation_reason = @reason
            WHERE id = @dealId",
            new { dealId, reason, cancelledBy });
    }

    public async Task<List<WaitQueueEntry>> GetWaitQueueAsync()
    {
        using var connection = new MySqlConnection(_connectionString);

        var entries = await connection.QueryAsync<WaitQueueEntry, AccountSimplified, WaitQueueEntry>(@"
            SELECT
                wq.account_id AS AccountId,
                wq.slots_needed AS SlotsNeeded,
                wq.entered_queue_on AS EnteredQueueOn,
                wq.next_slot_opens_on AS NextSlotOpensOn,
                wq.priority_score AS PriorityScore,
                -- Account info
                a.id AS AccountId,
                n.id AS NationId,
                n.nation_name AS NationName,
                n.ruler_name AS RulerName,
                al.name AS AllianceName
            FROM wait_queue wq
            JOIN account a ON wq.account_id = a.id
            JOIN cybernations_db.nation n ON a.nation_id = n.id
            LEFT JOIN cybernations_db.alliance al ON n.alliance_id = al.id
            ORDER BY wq.priority_score DESC",
            (entry, account) =>
            {
                entry.Account = account;
                return entry;
            },
            splitOn: "AccountId");

        return entries.ToList();
    }

    public async Task<List<DealFlagDetail>> GetFlaggedDealsAsync()
    {
        using var connection = new MySqlConnection(_connectionString);

        var flags = await connection.QueryAsync<DealFlagDetail, AccountSimplified, AccountSimplified, DealFlagDetail>(@"
            SELECT
                df.id AS Id,
                df.deal_id AS DealId,
                df.flag_type AS FlagType,
                df.flagged_on AS FlaggedOn,
                df.resolved_on AS ResolvedOn,
                df.resolved_by AS ResolvedBy,
                df.admin_notes AS AdminNotes,
                -- Flagged account
                a1.id AS AccountId,
                n1.id AS NationId,
                n1.nation_name AS NationName,
                n1.ruler_name AS RulerName,
                al1.name AS AllianceName,
                -- Innocent account
                a2.id AS AccountId,
                n2.id AS NationId,
                n2.nation_name AS NationName,
                n2.ruler_name AS RulerName,
                al2.name AS AllianceName
            FROM deal_flag df
            JOIN account a1 ON df.flagged_account_id = a1.id
            JOIN account a2 ON df.innocent_account_id = a2.id
            JOIN cybernations_db.nation n1 ON a1.nation_id = n1.id
            JOIN cybernations_db.nation n2 ON a2.nation_id = n2.id
            LEFT JOIN cybernations_db.alliance al1 ON n1.alliance_id = al1.id
            LEFT JOIN cybernations_db.alliance al2 ON n2.alliance_id = al2.id
            WHERE df.resolved_on IS NULL
            ORDER BY df.flagged_on DESC",
            (flag, flaggedAccount, innocentAccount) =>
            {
                flag.FlaggedAccount = flaggedAccount;
                flag.InnocentAccount = innocentAccount;
                return flag;
            },
            splitOn: "AccountId,AccountId");

        return flags.ToList();
    }

    public async Task ResolveDealFlagAsync(int flagId, string resolvedBy, string adminNotes)
    {
        using var connection = new MySqlConnection(_connectionString);

        await connection.ExecuteAsync(@"
            UPDATE deal_flag
            SET resolved_on = CURRENT_TIMESTAMP,
                resolved_by = @resolvedBy,
                admin_notes = @adminNotes
            WHERE id = @flagId",
            new { flagId, resolvedBy, adminNotes });
    }
}
