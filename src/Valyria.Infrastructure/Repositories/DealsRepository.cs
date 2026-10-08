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

        // Get active deals with my slot and partner slot info
        var deals = await connection.QueryAsync<DealDetail, SlotDetail, AccountSimplified, SlotDetail, AccountSimplified, DealDetail>(@"
            SELECT
                d.id AS Id,
                d.created_on AS CreatedOn,
                pa.discord AS PartnerDiscord,
                da.activity_date AS LastActivityDate,
                -- New deals with no activity yet count from the day the deal was created
                DATEDIFF(CURDATE(), COALESCE(da.activity_date, DATE(d.created_on))) AS DaysSinceActivity,
                CASE
                    WHEN DATEDIFF(CURDATE(), COALESCE(da.activity_date, DATE(d.created_on))) >= 5 THEN TRUE
                    ELSE FALSE
                END AS NeedsAction,
                CASE
                    WHEN DATEDIFF(CURDATE(), COALESCE(da.activity_date, DATE(d.created_on))) >= 5 THEN 'Send aid to maintain deal'
                    WHEN DATEDIFF(CURDATE(), COALESCE(da.activity_date, DATE(d.created_on))) >= 1 THEN 'Deal may become inactive soon'
                    ELSE NULL
                END AS ActionNeeded,
                -- My slot
                ms.id AS Id,
                ms.slot_number AS SlotNumber,
                ms.tech_sent_cycles AS TechSentCycles,
                ms.tech_received_cycles AS TechReceivedCycles,
                ms.cash_sent_cycles AS CashSentCycles,
                ms.cash_received_cycles AS CashReceivedCycles,
                TRUE AS HasActiveDeal,
                -- My account
                ma.id AS AccountId,
                mn.id AS NationId,
                mn.nation_name AS NationName,
                mn.ruler_name AS RulerName,
                mal.name AS AllianceName,
                -- Partner slot
                ps.id AS Id,
                ps.slot_number AS SlotNumber,
                ps.tech_sent_cycles AS TechSentCycles,
                ps.tech_received_cycles AS TechReceivedCycles,
                ps.cash_sent_cycles AS CashSentCycles,
                ps.cash_received_cycles AS CashReceivedCycles,
                TRUE AS HasActiveDeal,
                -- Partner account
                pa.id AS AccountId,
                pn.id AS NationId,
                pn.nation_name AS NationName,
                pn.ruler_name AS RulerName,
                pal.name AS AllianceName
            FROM deal d
            JOIN slot ms ON ms.id IN (d.slot_1_id, d.slot_2_id) AND ms.account_id = @accountId
            JOIN slot ps ON ps.id = IF(d.slot_1_id = ms.id, d.slot_2_id, d.slot_1_id)
            JOIN account ma ON ms.account_id = ma.id
            JOIN account pa ON ps.account_id = pa.id
            LEFT JOIN cybernations_db.nation mn ON ma.nation_id = mn.id
            LEFT JOIN cybernations_db.nation pn ON pa.nation_id = pn.id
            LEFT JOIN cybernations_db.alliance mal ON mn.alliance_id = mal.id
            LEFT JOIN cybernations_db.alliance pal ON pn.alliance_id = pal.id
            LEFT JOIN (
                SELECT deal_id, MAX(activity_date) AS activity_date
                FROM deal_activity
                GROUP BY deal_id
            ) da ON d.id = da.deal_id
            WHERE d.status = 'active'
            ORDER BY DaysSinceActivity DESC, ms.slot_number",
            (deal, mySlot, myAccount, partnerSlot, partnerAccount) =>
            {
                mySlot.Account = myAccount;
                partnerSlot.Account = partnerAccount;
                deal.MySlot = mySlot;
                deal.PartnerSlot = partnerSlot;
                return deal;
            },
            new { accountId },
            splitOn: "Id,AccountId,Id,AccountId");

        summary.ActiveDeals = deals.ToList();

        // Get notifications for flagged deals
        var notifications = await connection.QueryAsync<DealNotification>(@"
            SELECT
                CASE
                    WHEN df.flag_type = '5_day_replacement' THEN 'error'
                    WHEN df.flag_type = '1_day_inactive' THEN 'warning'
                    ELSE 'info'
                END AS Type,
                CASE
                    WHEN df.flag_type = '5_day_replacement' THEN
                        CONCAT('Your deal with ', n.nation_name, ' has had no aid for 5+ days and is being replaced.')
                    ELSE
                        CONCAT('Your deal with ', n.nation_name, ' has had no recent aid. Send or accept aid to keep it active.')
                END AS Message,
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

    public async Task<DealHistoryPage> GetAccountDealHistoryAsync(int accountId, int limit, int offset)
    {
        using var connection = new MySqlConnection(_connectionString);

        var totalCount = await connection.QuerySingleAsync<int>(@"
            SELECT COUNT(*)
            FROM deal d
            JOIN slot ms ON ms.id IN (d.slot_1_id, d.slot_2_id) AND ms.account_id = @accountId",
            new { accountId });

        var deals = await connection.QueryAsync<DealHistoryItem, AccountSimplified, DealHistoryItem>(@"
            SELECT
                d.id AS Id,
                d.status AS Status,
                d.created_on AS CreatedOn,
                d.cancelled_on AS CancelledOn,
                d.cancellation_reason AS CancellationReason,
                ms.slot_number AS MySlotNumber,
                (
                    SELECT COUNT(*) FROM deal_activity da
                    WHERE da.deal_id = d.id AND da.expected_sender_nation_id = ma.nation_id
                ) AS AidSentCount,
                (
                    SELECT COUNT(*) FROM deal_activity da
                    WHERE da.deal_id = d.id AND da.expected_receiver_nation_id = ma.nation_id
                ) AS AidReceivedCount,
                (
                    SELECT MAX(da.activity_date) FROM deal_activity da
                    WHERE da.deal_id = d.id
                ) AS LastActivityDate,
                -- Partner account
                pa.id AS AccountId,
                pn.id AS NationId,
                pn.nation_name AS NationName,
                pn.ruler_name AS RulerName,
                pal.name AS AllianceName
            FROM deal d
            JOIN slot ms ON ms.id IN (d.slot_1_id, d.slot_2_id) AND ms.account_id = @accountId
            JOIN slot ps ON ps.id = IF(d.slot_1_id = ms.id, d.slot_2_id, d.slot_1_id)
            JOIN account ma ON ms.account_id = ma.id
            JOIN account pa ON ps.account_id = pa.id
            LEFT JOIN cybernations_db.nation pn ON pa.nation_id = pn.id
            LEFT JOIN cybernations_db.alliance pal ON pn.alliance_id = pal.id
            ORDER BY d.created_on DESC, d.id DESC
            LIMIT @limit OFFSET @offset",
            (deal, partner) =>
            {
                deal.Partner = partner;
                return deal;
            },
            new { accountId, limit, offset },
            splitOn: "AccountId");

        return new DealHistoryPage
        {
            TotalCount = totalCount,
            Results = deals.ToList()
        };
    }

    public async Task<List<DealActivityDetail>> GetDealActivityAsync(int dealId, int accountId)
    {
        using var connection = new MySqlConnection(_connectionString);

        var myNationId = await connection.QueryFirstOrDefaultAsync<int?>(@"
            SELECT a.nation_id
            FROM deal d
            JOIN slot s ON s.id IN (d.slot_1_id, d.slot_2_id) AND s.account_id = @accountId
            JOIN account a ON s.account_id = a.id
            WHERE d.id = @dealId",
            new { dealId, accountId });

        if (myNationId == null)
        {
            return null;
        }

        var activity = await connection.QueryAsync<DealActivityDetail>(@"
            SELECT
                da.id AS Id,
                da.aid_id AS AidId,
                da.activity_date AS ActivityDate,
                da.expected_sender_nation_id = @myNationId AS IsOutgoing,
                COALESCE(aid.money, 0) AS Money,
                COALESCE(aid.technology, 0) AS Technology
            FROM deal_activity da
            LEFT JOIN cybernations_db.aid aid ON da.aid_id = aid.id
            WHERE da.deal_id = @dealId
            ORDER BY da.activity_date DESC, da.id DESC",
            new { dealId, myNationId });

        return activity.ToList();
    }

    public async Task<Deal> GetDealByIdAsync(int dealId)
    {
        using var connection = new MySqlConnection(_connectionString);

        var deal = await connection.QueryAsync<Deal, SlotDetail, AccountSimplified, SlotDetail, AccountSimplified, Deal>(@"
            SELECT
                d.id AS Id,
                d.slot_1_id AS Slot1Id,
                d.slot_2_id AS Slot2Id,
                d.status AS Status,
                d.created_on AS CreatedOn,
                d.created_by AS CreatedBy,
                d.cancelled_on AS CancelledOn,
                d.cancelled_by AS CancelledBy,
                d.cancellation_reason AS CancellationReason,
                -- Slot 1
                s1.id AS Id,
                s1.slot_number AS SlotNumber,
                s1.tech_sent_cycles AS TechSentCycles,
                s1.tech_received_cycles AS TechReceivedCycles,
                s1.cash_sent_cycles AS CashSentCycles,
                s1.cash_received_cycles AS CashReceivedCycles,
                EXISTS (
                    SELECT 1 FROM deal ad
                    WHERE s1.id IN (ad.slot_1_id, ad.slot_2_id) AND ad.status = 'active'
                ) AS HasActiveDeal,
                -- Slot 1 account
                a1.id AS AccountId,
                n1.id AS NationId,
                n1.nation_name AS NationName,
                n1.ruler_name AS RulerName,
                al1.name AS AllianceName,
                -- Slot 2
                s2.id AS Id,
                s2.slot_number AS SlotNumber,
                s2.tech_sent_cycles AS TechSentCycles,
                s2.tech_received_cycles AS TechReceivedCycles,
                s2.cash_sent_cycles AS CashSentCycles,
                s2.cash_received_cycles AS CashReceivedCycles,
                EXISTS (
                    SELECT 1 FROM deal ad
                    WHERE s2.id IN (ad.slot_1_id, ad.slot_2_id) AND ad.status = 'active'
                ) AS HasActiveDeal,
                -- Slot 2 account
                a2.id AS AccountId,
                n2.id AS NationId,
                n2.nation_name AS NationName,
                n2.ruler_name AS RulerName,
                al2.name AS AllianceName
            FROM deal d
            JOIN slot s1 ON d.slot_1_id = s1.id
            JOIN slot s2 ON d.slot_2_id = s2.id
            JOIN account a1 ON s1.account_id = a1.id
            JOIN account a2 ON s2.account_id = a2.id
            JOIN cybernations_db.nation n1 ON a1.nation_id = n1.id
            JOIN cybernations_db.nation n2 ON a2.nation_id = n2.id
            LEFT JOIN cybernations_db.alliance al1 ON n1.alliance_id = al1.id
            LEFT JOIN cybernations_db.alliance al2 ON n2.alliance_id = al2.id
            WHERE d.id = @dealId",
            (deal, slot1, account1, slot2, account2) =>
            {
                slot1.Account = account1;
                slot2.Account = account2;
                deal.Slot1 = slot1;
                deal.Slot2 = slot2;
                return deal;
            },
            new { dealId },
            splitOn: "Id,AccountId,Id,AccountId");

        return deal.FirstOrDefault();
    }

    public async Task<int> CreateDealAsync(CreateDealRequest request)
    {
        using var connection = new MySqlConnection(_connectionString);

        // Ensure slot_1_id < slot_2_id for constraint compatibility
        var slot1 = Math.Min(request.Slot1Id, request.Slot2Id);
        var slot2 = Math.Max(request.Slot1Id, request.Slot2Id);

        var dealId = await connection.QuerySingleAsync<int>(@"
            INSERT INTO deal (slot_1_id, slot_2_id, created_by)
            VALUES (@slot1, @slot2, @createdBy);
            SELECT LAST_INSERT_ID();",
            new
            {
                slot1,
                slot2,
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

    public async Task<AccountSlotsResponse> GetAccountSlotsAsync(int accountId)
    {
        using var connection = new MySqlConnection(_connectionString);

        var account = await connection.QueryFirstOrDefaultAsync<AccountSimplified>(@"
            SELECT
                a.id AS AccountId,
                n.id AS NationId,
                n.nation_name AS NationName,
                n.ruler_name AS RulerName,
                al.name AS AllianceName
            FROM account a
            LEFT JOIN cybernations_db.nation n ON a.nation_id = n.id
            LEFT JOIN cybernations_db.alliance al ON n.alliance_id = al.id
            WHERE a.id = @accountId",
            new { accountId });

        if (account == null)
        {
            return null;
        }

        var slots = await GetSlotsAsync(connection, accountId, availableOnly: false);
        slots.ForEach(slot => slot.Account = account);

        return new AccountSlotsResponse
        {
            AccountId = accountId,
            Account = account,
            Slots = slots
        };
    }

    public async Task<bool> UpdateSlotAsync(SlotUpdateRequest request, string updatedBy)
    {
        using var connection = new MySqlConnection(_connectionString);

        var rowsAffected = await connection.ExecuteAsync(@"
            UPDATE slot
            SET tech_sent_cycles = @TechSentCycles,
                tech_received_cycles = @TechReceivedCycles,
                cash_sent_cycles = @CashSentCycles,
                cash_received_cycles = @CashReceivedCycles,
                updated_by = @updatedBy
            WHERE id = @SlotId",
            new
            {
                request.SlotId,
                request.TechSentCycles,
                request.TechReceivedCycles,
                request.CashSentCycles,
                request.CashReceivedCycles,
                updatedBy
            });

        return rowsAffected > 0;
    }

    public async Task<List<SlotDetail>> GetAvailableSlotsAsync(int accountId)
    {
        using var connection = new MySqlConnection(_connectionString);

        return await GetSlotsAsync(connection, accountId, availableOnly: true);
    }

    private static async Task<List<SlotDetail>> GetSlotsAsync(IDbConnection connection, int accountId, bool availableOnly)
    {
        var slots = await connection.QueryAsync<SlotDetail>(@"
            SELECT *
            FROM (
                SELECT
                    s.id AS Id,
                    s.slot_number AS SlotNumber,
                    s.tech_sent_cycles AS TechSentCycles,
                    s.tech_received_cycles AS TechReceivedCycles,
                    s.cash_sent_cycles AS CashSentCycles,
                    s.cash_received_cycles AS CashReceivedCycles,
                    EXISTS (
                        SELECT 1 FROM deal d
                        WHERE s.id IN (d.slot_1_id, d.slot_2_id) AND d.status = 'active'
                    ) AS HasActiveDeal
                FROM slot s
                WHERE s.account_id = @accountId
            ) slots
            WHERE @availableOnly = FALSE OR HasActiveDeal = FALSE
            ORDER BY SlotNumber",
            new { accountId, availableOnly });

        return slots.ToList();
    }
}
