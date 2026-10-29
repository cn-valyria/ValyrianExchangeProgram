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

    // deal_state columns shared by the account and admin deal queries (aliases ds, and dd for
    // deal_deadline). The DB enum values ('awaiting_send') aren't valid C# enum names, so they're mapped
    // to the enums' numbers. CancelsOn is the deadline check_deal_inactivity cancels on, extensions included
    private const string DealStateColumns = @"
                CASE ds.state
                    WHEN 'pending_start' THEN 0
                    WHEN 'in_flight' THEN 1
                    WHEN 'awaiting_send' THEN 2
                    WHEN 'blocked' THEN 3
                END AS State,
                CASE ds.blocked_reason
                    WHEN 'sender_slots_full' THEN 0
                    WHEN 'receiver_slots_full' THEN 1
                    WHEN 'out_of_sequence_aid' THEN 2
                END AS BlockedReason,
                ds.state_since AS StateSince,
                ds.in_flight_until AS InFlightUntil,
                DATEDIFF(DATE(CONVERT_TZ(UTC_TIMESTAMP(), '+00:00', '-05:00')), ds.responsible_since) AS DaysResponsible,
                dd.cancels_on AS CancelsOn,
                CASE dd.extension_kind
                    WHEN 'five_days' THEN 0
                    WHEN 'until_next_leg' THEN 1
                END AS ExtensionKind,
                dd.extended_by AS ExtendedBy,";

    // Team bonus columns for a slot alias, read from team_bonus_slot joined as {slot}tb (see
    // TeamBonusJoin). The view only lists bonus slots in effect, so the dates are null once a bonus ends
    private static string TeamBonusColumns(string slot) => $@"
                {slot}.is_team_bonus AS IsTeamBonus,
                {slot}tb.ends_on AS TeamBonusEndsOn,
                {slot}tb.last_match_on AS TeamBonusMatchableUntil,";

    private static string TeamBonusJoin(string slot) => $@"
            LEFT JOIN team_bonus_slot {slot}tb ON {slot}tb.slot_id = {slot}.id";

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
                da.activity_date AS LastActivityDate," + DealStateColumns + @"
                COALESCE(ds.next_sender_account_id = ma.id, FALSE) AS IsMyTurnToSend,
                COALESCE(ds.responsible_account_id = ma.id, FALSE) AS IAmResponsible,
                COALESCE(ds.responsible_account_id = pa.id, FALSE) AS PartnerIsResponsible,
                COALESCE(fl.expected_receiver_nation_id = ma.nation_id, FALSE) AS InFlightIsIncoming,
                COALESCE(fl.approved_on IS NOT NULL OR fl.linked_late, FALSE) AS InFlightAccepted,
                DATE(ba.date) + INTERVAL 10 DAY AS BlockedUntil,
                f.flagged_on AS FlaggedOn,
                -- My slot
                ms.id AS Id,
                ms.slot_number AS SlotNumber,
                ms.tech_sent_cycles AS TechSentCycles,
                ms.tech_received_cycles AS TechReceivedCycles,
                ms.cash_sent_cycles AS CashSentCycles,
                ms.cash_received_cycles AS CashReceivedCycles,
                TRUE AS HasActiveDeal," + TeamBonusColumns("ms") + @"
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
                TRUE AS HasActiveDeal," + TeamBonusColumns("ps") + @"
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
            JOIN account pa ON ps.account_id = pa.id" + TeamBonusJoin("ms") + TeamBonusJoin("ps") + @"
            LEFT JOIN cybernations_db.nation mn ON ma.nation_id = mn.id
            LEFT JOIN cybernations_db.nation pn ON pa.nation_id = pn.id
            LEFT JOIN cybernations_db.alliance mal ON mn.alliance_id = mal.id
            LEFT JOIN cybernations_db.alliance pal ON pn.alliance_id = pal.id
            LEFT JOIN (
                SELECT deal_id, MAX(activity_date) AS activity_date
                FROM deal_activity
                GROUP BY deal_id
            ) da ON d.id = da.deal_id
            LEFT JOIN deal_state ds ON ds.deal_id = d.id
            LEFT JOIN deal_deadline dd ON dd.deal_id = ds.deal_id
            -- The leg currently active between the pair, and any out-of-sequence aid blocking it
            LEFT JOIN deal_activity fl ON fl.aid_id = ds.in_flight_aid_id
            LEFT JOIN cybernations_db.aid ba ON ba.id = ds.blocking_aid_id
            -- Open warning against this account for holding up the deal
            LEFT JOIN (
                SELECT deal_id, MIN(flagged_on) AS flagged_on
                FROM deal_flag
                WHERE flag_type = '1_day_inactive'
                    AND resolved_on IS NULL
                    AND flagged_account_id = @accountId
                GROUP BY deal_id
            ) f ON f.deal_id = d.id
            WHERE d.status = 'active'
            ORDER BY ms.slot_number",
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

        summary.TeamEvent = await connection.QueryFirstOrDefaultAsync<AccountTeamEvent>(@"
            SELECT
                team AS Team,
                approved_on AS ApprovedOn,
                ends_on AS EndsOn,
                last_match_on AS BonusSlotMatchableUntil
            FROM team_bonus_slot
            WHERE account_id = @accountId",
            new { accountId });

        // Notifications for deals that were cancelled over a flag. Warnings on active deals show
        // on the deals themselves. Flagged accounts hear about it until an admin resolves the flag;
        // partners of a replaced deal hear about it for 10 days. aid_failed flags both accounts,
        // so each only needs its own row
        var cancelledDealFlags = await connection.QueryAsync<CancelledDealFlag>(@"
            SELECT
                df.deal_id AS DealId,
                df.flag_type AS FlagType,
                df.flag_reason AS FlagReason,
                df.flagged_account_id = @accountId AS FlaggedMe,
                n.nation_name AS PartnerNationName
            FROM deal_flag df
            JOIN account partner_acct ON partner_acct.id =
                IF(df.flagged_account_id = @accountId, df.innocent_account_id, df.flagged_account_id)
            LEFT JOIN cybernations_db.nation n ON partner_acct.nation_id = n.id
            WHERE df.resolved_on IS NULL
                AND df.flag_type IN ('5_day_replacement', 'aid_failed')
                AND (
                    df.flagged_account_id = @accountId
                    OR (
                        df.innocent_account_id = @accountId
                        AND df.flag_type = '5_day_replacement'
                        AND df.flagged_on >= CURRENT_TIMESTAMP - INTERVAL 10 DAY
                    )
                )
            ORDER BY df.flagged_on DESC",
            new { accountId });

        summary.Notifications = cancelledDealFlags.Select(ToNotification).ToList();

        return summary;
    }

    private class CancelledDealFlag
    {
        public int DealId { get; set; }
        public string FlagType { get; set; } = string.Empty;
        public string? FlagReason { get; set; }
        public bool FlaggedMe { get; set; }
        public string? PartnerNationName { get; set; }
    }

    private static DealNotification ToNotification(CancelledDealFlag flag)
    {
        var partner = flag.PartnerNationName ?? "your partner";
        const string matchingPaused = "You won't be matched with new partners until VEP management reviews it.";

        string message;
        if (flag.FlagType == "aid_failed")
        {
            var whatHappened = flag.FlagReason == "aid_expired" ? "expired without being accepted" : "was cancelled";
            message = $"Your deal with {partner} was cancelled because an aid offer between you {whatHappened}. {matchingPaused}";
        }
        else if (flag.FlaggedMe)
        {
            var cause = flag.FlagReason switch
            {
                "sender_slots_full" or "receiver_slots_full" => "your aid slots were full of other aid",
                "out_of_sequence_aid" => "aid you sent out of turn blocked it",
                _ => "you didn't send the expected aid"
            };
            message = $"Your deal with {partner} was cancelled because {cause} for 5 days. {matchingPaused}";
        }
        else
        {
            var cause = flag.FlagReason switch
            {
                "sender_slots_full" or "receiver_slots_full" => "their aid slots were full of other aid",
                "out_of_sequence_aid" => "aid they sent out of turn blocked it",
                _ => "they didn't send the expected aid"
            };
            message = $"Your deal with {partner} was cancelled because {cause} for 5 days. Your slot will be matched with a new partner.";
        }

        return new DealNotification
        {
            Type = flag.FlaggedMe ? "error" : "info",
            Message = message,
            DealId = flag.DealId
        };
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
                COALESCE(aid.technology, 0) AS Technology,
                -- Legs linked after their window closed count as accepted (see V9)
                CASE
                    WHEN da.failure_reason = 'cancelled' THEN 2
                    WHEN da.failure_reason = 'expired' THEN 3
                    WHEN da.approved_on IS NOT NULL OR da.linked_late THEN 1
                    ELSE 0
                END AS Outcome
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
                ) AS HasActiveDeal," + TeamBonusColumns("s1") + @"
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
                ) AS HasActiveDeal," + TeamBonusColumns("s2") + @"
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
            LEFT JOIN cybernations_db.alliance al2 ON n2.alliance_id = al2.id" + TeamBonusJoin("s1") + TeamBonusJoin("s2") + @"
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

    public async Task<AdminDealPage> GetActiveDealsAsync(string? search, int limit, int offset)
    {
        using var connection = new MySqlConnection(_connectionString);

        var fromClause = @"
            FROM deal d
            JOIN slot s1 ON d.slot_1_id = s1.id
            JOIN slot s2 ON d.slot_2_id = s2.id
            JOIN account a1 ON s1.account_id = a1.id
            JOIN account a2 ON s2.account_id = a2.id" + TeamBonusJoin("s1") + TeamBonusJoin("s2") + @"
            LEFT JOIN cybernations_db.nation n1 ON a1.nation_id = n1.id
            LEFT JOIN cybernations_db.nation n2 ON a2.nation_id = n2.id
            LEFT JOIN cybernations_db.alliance al1 ON n1.alliance_id = al1.id
            LEFT JOIN cybernations_db.alliance al2 ON n2.alliance_id = al2.id
            LEFT JOIN deal_state ds ON ds.deal_id = d.id
            LEFT JOIN deal_deadline dd ON dd.deal_id = ds.deal_id
            WHERE d.status = 'active'
                AND (
                    @search IS NULL
                    OR n1.nation_name LIKE CONCAT('%', @search, '%')
                    OR n1.ruler_name LIKE CONCAT('%', @search, '%')
                    OR al1.name LIKE CONCAT('%', @search, '%')
                    OR n2.nation_name LIKE CONCAT('%', @search, '%')
                    OR n2.ruler_name LIKE CONCAT('%', @search, '%')
                    OR al2.name LIKE CONCAT('%', @search, '%')
                )";

        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        var totalCount = await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) " + fromClause,
            new { search });

        var deals = await connection.QueryAsync<AdminDealItem, SlotDetail, AccountSimplified, SlotDetail, AccountSimplified, AdminDealItem>(@"
            SELECT
                d.id AS Id,
                d.created_on AS CreatedOn,
                d.created_by AS CreatedBy,
                (
                    SELECT MAX(da.activity_date) FROM deal_activity da
                    WHERE da.deal_id = d.id
                ) AS LastActivityDate," + DealStateColumns + @"
                ds.next_sender_account_id AS NextSenderAccountId,
                ds.responsible_account_id AS ResponsibleAccountId,
                EXISTS (
                    SELECT 1 FROM deal_flag df
                    WHERE df.deal_id = d.id AND df.resolved_on IS NULL
                ) AS HasOpenFlag,
                -- Slot 1
                s1.id AS Id,
                s1.slot_number AS SlotNumber,
                s1.tech_sent_cycles AS TechSentCycles,
                s1.tech_received_cycles AS TechReceivedCycles,
                s1.cash_sent_cycles AS CashSentCycles,
                s1.cash_received_cycles AS CashReceivedCycles,
                TRUE AS HasActiveDeal," + TeamBonusColumns("s1") + @"
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
                TRUE AS HasActiveDeal," + TeamBonusColumns("s2") + @"
                -- Slot 2 account
                a2.id AS AccountId,
                n2.id AS NationId,
                n2.nation_name AS NationName,
                n2.ruler_name AS RulerName,
                al2.name AS AllianceName
            " + fromClause + @"
            -- Deals held up the longest first
            ORDER BY ds.responsible_since IS NULL, ds.responsible_since, d.id
            LIMIT @limit OFFSET @offset",
            (deal, slot1, account1, slot2, account2) =>
            {
                slot1.Account = account1;
                slot2.Account = account2;
                deal.Slot1 = slot1;
                deal.Slot2 = slot2;
                return deal;
            },
            new { search, limit, offset },
            splitOn: "Id,AccountId,Id,AccountId");

        return new AdminDealPage
        {
            TotalCount = totalCount,
            Results = deals.ToList()
        };
    }

    public async Task<AidActivityPage> SearchAidActivityAsync(AidActivityFilters filters, int limit, int offset)
    {
        using var connection = new MySqlConnection(_connectionString);

        // Any aid with a VEP account on at least one side
        const string fromClause = @"
            FROM cybernations_db.aid aid
            LEFT JOIN account sa ON aid.sending_nation_id = sa.nation_id
            LEFT JOIN account ra ON aid.receiving_nation_id = ra.nation_id
            LEFT JOIN cybernations_db.nation sn ON aid.sending_nation_id = sn.id
            LEFT JOIN cybernations_db.nation rn ON aid.receiving_nation_id = rn.id
            LEFT JOIN cybernations_db.alliance sal ON sn.alliance_id = sal.id
            LEFT JOIN cybernations_db.alliance ral ON rn.alliance_id = ral.id
            WHERE (sa.id IS NOT NULL OR ra.id IS NOT NULL)
                AND (@includeOnHold OR (COALESCE(sa.va, '') <> 'H' AND COALESCE(ra.va, '') <> 'H'))
                AND (NOT @unmatchedOnly OR NOT EXISTS (SELECT 1 FROM deal_activity da WHERE da.aid_id = aid.id))
                AND (@sentSince IS NULL OR aid.date >= @sentSince)
                AND (@sentUntil IS NULL OR aid.date < DATE_ADD(@sentUntil, INTERVAL 1 DAY))
                AND (
                    @search IS NULL
                    OR (@direction <> 'received' AND (
                        sn.nation_name LIKE CONCAT('%', @search, '%')
                        OR sn.ruler_name LIKE CONCAT('%', @search, '%')
                        OR sal.name LIKE CONCAT('%', @search, '%')
                    ))
                    OR (@direction <> 'sent' AND (
                        rn.nation_name LIKE CONCAT('%', @search, '%')
                        OR rn.ruler_name LIKE CONCAT('%', @search, '%')
                        OR ral.name LIKE CONCAT('%', @search, '%')
                    ))
                )";

        var parameters = new
        {
            search = string.IsNullOrWhiteSpace(filters.Search) ? null : filters.Search.Trim(),
            direction = filters.Direction?.ToLowerInvariant() ?? "",
            sentSince = filters.SentSince?.Date,
            sentUntil = filters.SentUntil?.Date,
            includeOnHold = filters.IncludeOnHold,
            unmatchedOnly = filters.UnmatchedOnly,
            limit,
            offset
        };

        var totalCount = await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) " + fromClause,
            parameters);

        var results = await connection.QueryAsync<AidActivityItem, AidParty, AidParty, AidActivityItem>(@"
            SELECT
                aid.id AS AidId,
                aid.date AS SentOn,
                aid.status AS Status,
                aid.money AS Money,
                aid.technology AS Technology,
                aid.soldiers AS Soldiers,
                aid.reason AS Reason,
                (SELECT MIN(da.deal_id) FROM deal_activity da WHERE da.aid_id = aid.id) AS DealId,
                -- Sender
                aid.sending_nation_id AS NationId,
                sn.nation_name AS NationName,
                sn.ruler_name AS RulerName,
                sal.name AS AllianceName,
                sa.id AS AccountId,
                sa.va AS Role,
                -- Receiver
                aid.receiving_nation_id AS NationId,
                rn.nation_name AS NationName,
                rn.ruler_name AS RulerName,
                ral.name AS AllianceName,
                ra.id AS AccountId,
                ra.va AS Role
            " + fromClause + @"
            ORDER BY aid.date DESC, aid.id DESC
            LIMIT @limit OFFSET @offset",
            (item, sender, receiver) =>
            {
                item.Sender = sender;
                item.Receiver = receiver;
                return item;
            },
            parameters,
            splitOn: "NationId,NationId");

        return new AidActivityPage
        {
            TotalCount = totalCount,
            Results = results.ToList()
        };
    }

    public async Task<List<DealFlagDetail>> GetFlaggedDealsAsync()
    {
        using var connection = new MySqlConnection(_connectionString);

        var flags = await QueryOpenFlagsAsync(connection, "TRUE", null);

        return flags.ToList();
    }

    /// <summary>
    /// Open flags matching the condition (alias df), newest first, with their deal, both slots and the deal's
    /// current state
    /// </summary>
    private static Task<IEnumerable<DealFlagDetail>> QueryOpenFlagsAsync(IDbConnection connection, string condition,
        object? param, IDbTransaction? transaction = null) =>
        connection.QueryAsync<DealFlagDetail, SlotDetail, AccountSimplified, SlotDetail, AccountSimplified, DealFlagDetail>(@"
            SELECT
                df.id AS Id,
                df.deal_id AS DealId,
                -- The enum's DB values ('1_day_inactive') aren't valid C# enum names
                CASE df.flag_type
                    WHEN '1_day_inactive' THEN 0
                    WHEN '5_day_replacement' THEN 1
                    WHEN 'aid_failed' THEN 2
                END AS FlagType,
                CASE df.flag_reason
                    WHEN 'awaiting_send' THEN 0
                    WHEN 'sender_slots_full' THEN 1
                    WHEN 'receiver_slots_full' THEN 2
                    WHEN 'out_of_sequence_aid' THEN 3
                    WHEN 'aid_cancelled' THEN 4
                    WHEN 'aid_expired' THEN 5
                END AS FlagReason,
                df.flagged_on AS FlaggedOn,
                df.resolved_on AS ResolvedOn,
                df.resolved_by AS ResolvedBy,
                df.admin_notes AS AdminNotes,
                fa.va AS FlaggedAccountRole,
                ia.va AS InnocentAccountRole,
                (
                    SELECT COUNT(*) FROM deal_flag o
                    WHERE o.flagged_account_id = df.flagged_account_id AND o.resolved_on IS NULL
                ) AS FlaggedAccountOpenFlagCount,
                d.status AS DealStatus,
                d.created_on AS DealCreatedOn,
                d.cancelled_on AS DealCancelledOn,
                d.cancelled_by AS DealCancelledBy,
                d.cancellation_reason AS CancellationReason," + DealStateColumns + @"
                ds.next_sender_account_id AS NextSenderAccountId,
                ds.responsible_account_id AS ResponsibleAccountId,
                DATE(ba.date) + INTERVAL 10 DAY AS BlockedUntil,
                (
                    SELECT COUNT(*) FROM deal_activity da
                    WHERE da.deal_id = d.id AND da.failed_on IS NULL
                ) AS LegsCompleted,
                (
                    SELECT MAX(da.activity_date) FROM deal_activity da
                    WHERE da.deal_id = d.id AND da.failed_on IS NULL AND da.expected_sender_nation_id = fa.nation_id
                ) AS FlaggedLastSentOn,
                -- What would stop the deal being restored
                (
                    SELECT MIN(od.id) FROM deal od
                    WHERE od.status = 'active' AND od.id <> d.id AND fs.id IN (od.slot_1_id, od.slot_2_id)
                ) AS FlaggedSlotOtherDealId,
                (
                    SELECT MIN(od.id) FROM deal od
                    WHERE od.status = 'active' AND od.id <> d.id AND ps.id IN (od.slot_1_id, od.slot_2_id)
                ) AS InnocentSlotOtherDealId,
                (
                    SELECT MIN(dp.deal_id) FROM deal_participants dp
                    WHERE dp.status = 'active' AND dp.deal_id <> d.id
                        AND ((dp.account_1_id = fa.id AND dp.account_2_id = ia.id)
                            OR (dp.account_1_id = ia.id AND dp.account_2_id = fa.id))
                ) AS PairOtherDealId,
                fa.va = 'H' AS FlaggedAccountOnHold,
                ia.va = 'H' AS InnocentAccountOnHold,
                -- Flagged slot
                fs.id AS Id,
                fs.slot_number AS SlotNumber,
                fs.tech_sent_cycles AS TechSentCycles,
                fs.tech_received_cycles AS TechReceivedCycles,
                fs.cash_sent_cycles AS CashSentCycles,
                fs.cash_received_cycles AS CashReceivedCycles,
                d.status = 'active' AS HasActiveDeal," + TeamBonusColumns("fs") + @"
                -- Flagged account. Its nation may have been deleted since
                fa.id AS AccountId,
                fa.nation_id AS NationId,
                fn.nation_name AS NationName,
                fn.ruler_name AS RulerName,
                fal.name AS AllianceName,
                -- Innocent slot
                ps.id AS Id,
                ps.slot_number AS SlotNumber,
                ps.tech_sent_cycles AS TechSentCycles,
                ps.tech_received_cycles AS TechReceivedCycles,
                ps.cash_sent_cycles AS CashSentCycles,
                ps.cash_received_cycles AS CashReceivedCycles,
                d.status = 'active' AS HasActiveDeal," + TeamBonusColumns("ps") + @"
                -- Innocent account
                ia.id AS AccountId,
                ia.nation_id AS NationId,
                pn.nation_name AS NationName,
                pn.ruler_name AS RulerName,
                pal.name AS AllianceName
            FROM deal_flag df
            JOIN deal d ON d.id = df.deal_id
            JOIN account fa ON df.flagged_account_id = fa.id
            JOIN account ia ON df.innocent_account_id = ia.id
            JOIN slot fs ON fs.id IN (d.slot_1_id, d.slot_2_id) AND fs.account_id = fa.id
            JOIN slot ps ON ps.id IN (d.slot_1_id, d.slot_2_id) AND ps.id <> fs.id" + TeamBonusJoin("fs") + TeamBonusJoin("ps") + @"
            LEFT JOIN cybernations_db.nation fn ON fa.nation_id = fn.id
            LEFT JOIN cybernations_db.nation pn ON ia.nation_id = pn.id
            LEFT JOIN cybernations_db.alliance fal ON fn.alliance_id = fal.id
            LEFT JOIN cybernations_db.alliance pal ON pn.alliance_id = pal.id
            -- A cancelled deal keeps its deal_state row until the next update_deal_state run
            LEFT JOIN deal_state ds ON ds.deal_id = d.id AND d.status = 'active'
            LEFT JOIN deal_deadline dd ON dd.deal_id = ds.deal_id
            LEFT JOIN cybernations_db.aid ba ON ba.id = ds.blocking_aid_id
            WHERE df.resolved_on IS NULL AND (" + condition + @")
            ORDER BY df.flagged_on DESC, df.id DESC",
            (flag, flaggedSlot, flaggedAccount, innocentSlot, innocentAccount) =>
            {
                flaggedSlot.Account = flaggedAccount;
                innocentSlot.Account = innocentAccount;
                flag.FlaggedSlot = flaggedSlot;
                flag.InnocentSlot = innocentSlot;
                flag.FlaggedAccount = flaggedAccount;
                flag.InnocentAccount = innocentAccount;
                return flag;
            },
            param,
            transaction,
            splitOn: "Id,AccountId,Id,AccountId");

    private class FlagEvidenceContext
    {
        public int DealId { get; set; }
        public bool DealIsActive { get; set; }
        public DateTime DealCreatedOn { get; set; }
        public DateTime FlaggedOn { get; set; }
        public int FlaggedNationId { get; set; }
        public int FlaggedSlotsTotal { get; set; }
        public DateTime SlotsAsOf { get; set; }
    }

    public async Task<DealFlagEvidence> GetDealFlagEvidenceAsync(int flagId)
    {
        using var connection = new MySqlConnection(_connectionString);

        var context = await connection.QueryFirstOrDefaultAsync<FlagEvidenceContext>(@"
            SELECT
                df.deal_id AS DealId,
                d.status = 'active' AS DealIsActive,
                d.created_on AS DealCreatedOn,
                df.flagged_on AS FlaggedOn,
                fa.nation_id AS FlaggedNationId,
                fst.slots_total AS FlaggedSlotsTotal,
                IF(d.status = 'active',
                    DATE(CONVERT_TZ(UTC_TIMESTAMP(), '+00:00', '-05:00')),
                    DATE(CONVERT_TZ(df.flagged_on, '+00:00', '-05:00'))) AS SlotsAsOf
            FROM deal_flag df
            JOIN deal d ON d.id = df.deal_id
            JOIN account fa ON fa.id = df.flagged_account_id
            -- Today's total, team bonus included: the flagged account's team as of the flag isn't kept
            JOIN account_slot_total fst ON fst.account_id = fa.id
            WHERE df.id = @flagId",
            new { flagId });

        if (context == null)
        {
            return null;
        }

        // Same outcome rules as GetDealActivityAsync
        var legs = await connection.QueryAsync<FlagDealLeg>(@"
            SELECT
                da.leg_number AS LegNumber,
                da.aid_id AS AidId,
                da.activity_date AS ActivityDate,
                da.expected_sender_nation_id = @FlaggedNationId AS SentByFlaggedAccount,
                COALESCE(aid.money, 0) AS Money,
                COALESCE(aid.technology, 0) AS Technology,
                CASE
                    WHEN da.failure_reason = 'cancelled' THEN 2
                    WHEN da.failure_reason = 'expired' THEN 3
                    WHEN da.approved_on IS NOT NULL OR da.linked_late THEN 1
                    ELSE 0
                END AS Outcome,
                da.failure_excused AS FailureExcused
            FROM deal_activity da
            LEFT JOIN cybernations_db.aid aid ON da.aid_id = aid.id
            WHERE da.deal_id = @DealId
            ORDER BY da.leg_number",
            context);

        // What held the nation's slots, using the same window as update_deal_state. For a deal that has
        // ended, it's rebuilt as of the run that raised the flag, as in V13 (status 4 included, since the
        // loader expires aid after its window)
        var slotAid = await connection.QueryAsync<FlagSlotAid, NationSimplified, FlagSlotAid>(@"
            SELECT
                aid.id AS AidId,
                aid.date AS SentOn,
                aid.sending_nation_id = @FlaggedNationId AS IsOutgoing,
                aid.status AS Status,
                aid.money AS Money,
                aid.technology AS Technology,
                aid.soldiers AS Soldiers,
                aid.reason AS Reason,
                (SELECT MIN(da.deal_id) FROM deal_activity da WHERE da.aid_id = aid.id) AS DealId,
                aid.date < CONVERT_TZ(@DealCreatedOn, '+00:00', '-05:00') AS SentBeforeDeal,
                IF(aid.sending_nation_id = @FlaggedNationId, aid.receiving_nation_id, aid.sending_nation_id) AS NationId,
                n.nation_name AS NationName,
                n.ruler_name AS RulerName,
                al.name AS AllianceName
            FROM cybernations_db.aid aid
            LEFT JOIN cybernations_db.nation n
                ON n.id = IF(aid.sending_nation_id = @FlaggedNationId, aid.receiving_nation_id, aid.sending_nation_id)
            LEFT JOIN cybernations_db.alliance al ON n.alliance_id = al.id
            WHERE @FlaggedNationId IN (aid.sending_nation_id, aid.receiving_nation_id)
                AND DATE(aid.date) > @SlotsAsOf - INTERVAL 10 DAY
                AND IF(@DealIsActive,
                    aid.status IN (1, 2),
                    aid.status IN (1, 2, 4) AND aid.date < CONVERT_TZ(@FlaggedOn, '+00:00', '-05:00'))
            ORDER BY aid.date, aid.id",
            (aid, otherNation) =>
            {
                aid.OtherNation = otherNation;
                return aid;
            },
            context,
            splitOn: "NationId");

        return new DealFlagEvidence
        {
            Legs = legs.ToList(),
            SlotsAsOf = context.SlotsAsOf,
            FlaggedSlotsTotal = context.FlaggedSlotsTotal,
            FlaggedSlotAid = slotAid.ToList()
        };
    }

    public async Task<AccountDealFlag> GetAccountDealFlagAsync(int dealId, int accountId)
    {
        using var connection = new MySqlConnection(_connectionString);

        // The same warning GetAccountDealsAsync reports as the deal's FlaggedOn: the oldest, and flags come newest first
        var flag = (await QueryOpenFlagsAsync(connection, @"
                df.deal_id = @dealId
                AND df.flagged_account_id = @accountId
                AND df.flag_type = '1_day_inactive'
                AND d.status = 'active'",
            new { dealId, accountId })).LastOrDefault();

        if (flag == null)
        {
            return null;
        }

        // Notes are written for other admins, not the account
        flag.AdminNotes = null;

        return new AccountDealFlag
        {
            Flag = flag,
            Evidence = await GetDealFlagEvidenceAsync(flag.Id)
        };
    }

    private class ExtensionBase
    {
        public int LegsCounted { get; set; }
        public DateTime Deadline { get; set; }
    }

    public async Task<DealExtendResult> ExtendFlaggedDealAsync(int flagId, DealExtensionKind kind, string grantedBy, string? adminNotes)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = await connection.BeginTransactionAsync();

        var flag = (await QueryOpenFlagsAsync(connection, "df.id = @flagId", new { flagId }, transaction)).FirstOrDefault();
        if (flag == null)
        {
            return DealExtendResult.FlagNotFound;
        }

        if (!flag.CanExtendDeal)
        {
            return DealExtendResult.NotExtendable;
        }

        // Hold the deal's state still while the extension is worked out from it
        await connection.ExecuteAsync(
            "SELECT 1 FROM deal_state WHERE deal_id = @dealId FOR UPDATE",
            new { dealId = flag.DealId },
            transaction);

        // Five more days runs from the current deadline, which may already be extended. A deal with no
        // deadline (held off until the next leg) gets five days from today
        var current = await connection.QuerySingleAsync<ExtensionBase>(@"
            SELECT
                ds.legs_counted AS LegsCounted,
                COALESCE(dd.cancels_on, DATE(CONVERT_TZ(UTC_TIMESTAMP(), '+00:00', '-05:00'))) AS Deadline
            FROM deal_state ds
            JOIN deal_deadline dd ON dd.deal_id = ds.deal_id
            WHERE ds.deal_id = @dealId",
            new { dealId = flag.DealId },
            transaction);

        DateTime? extendedUntil = kind == DealExtensionKind.FiveDays ? current.Deadline.AddDays(5) : null;
        var granted = kind == DealExtensionKind.FiveDays
            ? $"Given 5 more days, until {extendedUntil:yyyy-MM-dd}"
            : "Given until the next aid is recorded";
        var notes = string.IsNullOrWhiteSpace(adminNotes) ? granted : $"{granted}: {adminNotes.Trim()}";

        await connection.ExecuteAsync(@"
            UPDATE deal_extension
            SET ended_on = CURRENT_TIMESTAMP,
                end_reason = 'superseded'
            WHERE deal_id = @dealId AND ended_on IS NULL;

            INSERT INTO deal_extension (deal_id, flag_id, account_id, kind, extended_until, legs_counted_at_grant,
                granted_by, admin_notes)
            VALUES (@dealId, @flagId, @accountId, @kind, @extendedUntil, @legsCounted, @grantedBy, @adminNotes);

            UPDATE deal_flag
            SET resolved_on = CURRENT_TIMESTAMP,
                resolved_by = @grantedBy,
                admin_notes = @notes
            WHERE id = @flagId;",
            new
            {
                dealId = flag.DealId,
                flagId,
                accountId = flag.FlaggedAccount.AccountId,
                kind = kind == DealExtensionKind.FiveDays ? "five_days" : "until_next_leg",
                extendedUntil,
                legsCounted = current.LegsCounted,
                grantedBy,
                adminNotes = string.IsNullOrWhiteSpace(adminNotes) ? null : adminNotes.Trim(),
                notes
            },
            transaction);

        await transaction.CommitAsync();

        return DealExtendResult.Extended;
    }

    public async Task<DealRestoreResult> RestoreFlaggedDealAsync(int flagId, string restoredBy, string? adminNotes)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = await connection.BeginTransactionAsync();

        var flag = (await QueryOpenFlagsAsync(connection, "df.id = @flagId", new { flagId }, transaction)).FirstOrDefault();
        if (flag == null)
        {
            return DealRestoreResult.FlagNotFound;
        }

        if (!flag.CanRestoreDeal)
        {
            return DealRestoreResult.NotRestorable;
        }

        var notes = string.IsNullOrWhiteSpace(adminNotes) ? "Deal restored" : $"Deal restored: {adminNotes.Trim()}";

        // Dropping the deal's old state restarts its 5-day clock when the data loader next rebuilds it.
        // Failed legs are excused so check_deal_inactivity doesn't cancel the deal over them again (V15).
        // Every open flag on the deal goes, including any warning left over from before it was cancelled
        await connection.ExecuteAsync(@"
            UPDATE deal
            SET status = 'active',
                cancelled_on = NULL,
                cancelled_by = NULL,
                cancellation_reason = NULL
            WHERE id = @dealId;

            DELETE FROM deal_state WHERE deal_id = @dealId;

            UPDATE deal_extension
            SET ended_on = CURRENT_TIMESTAMP,
                end_reason = 'deal_ended'
            WHERE deal_id = @dealId AND ended_on IS NULL;

            UPDATE deal_activity
            SET failure_excused = TRUE
            WHERE deal_id = @dealId AND failed_on IS NOT NULL;

            UPDATE deal_flag
            SET resolved_on = CURRENT_TIMESTAMP,
                resolved_by = @restoredBy,
                admin_notes = @notes
            WHERE deal_id = @dealId AND resolved_on IS NULL;",
            new { dealId = flag.DealId, restoredBy, notes },
            transaction);

        await transaction.CommitAsync();

        return DealRestoreResult.Restored;
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
                    ) AS HasActiveDeal," + TeamBonusColumns("s").TrimEnd(',') + @"
                FROM slot s" + TeamBonusJoin("s") + @"
                -- A bonus slot whose event has ended is kept only for its deal history
                WHERE s.account_id = @accountId
                    AND (NOT s.is_team_bonus OR stb.slot_id IS NOT NULL)
            ) slots
            WHERE @availableOnly = FALSE OR HasActiveDeal = FALSE
            ORDER BY SlotNumber",
            new { accountId, availableOnly });

        return slots.ToList();
    }
}
