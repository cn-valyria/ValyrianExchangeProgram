using Dapper;
using MySql.Data.MySqlClient;

namespace Valyria.IntegrationTests;

/// <summary>
/// Seeds rows for repository tests. Columns the tests don't care about get neutral defaults.
/// </summary>
public sealed class TestData(string connectionString)
{
    public async Task<T> ScalarAsync<T>(string sql, object? param = null)
    {
        using var connection = new MySqlConnection(connectionString);
        return await connection.ExecuteScalarAsync<T>(sql, param) ?? default!;
    }

    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null)
    {
        using var connection = new MySqlConnection(connectionString);
        return await connection.QueryAsync<T>(sql, param);
    }

    public async Task ExecuteAsync(string sql, object? param = null)
    {
        using var connection = new MySqlConnection(connectionString);
        await connection.ExecuteAsync(sql, param);
    }

    /// <summary>
    /// The CN date the deal procedures and repositories use
    /// </summary>
    public Task<DateTime> CnTodayAsync() => ScalarAsync<DateTime>("select DATE(CONVERT_TZ(UTC_TIMESTAMP(), '+00:00', '-05:00'))");

    public Task AllianceAsync(int id, string name) => ExecuteAsync(@"
        insert into cybernations_db.alliance (id, name, total_nations, active_nations, percent_active, strength,
            average_strength, score, land, infrastructure, technology, war, peace, soldiers, tanks, cruise, nukes,
            aircraft, navy, anarchy)
        values (@id, @name, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)", new { id, name });

    public Task NationAsync(int id, string nationName, string rulerName, int? allianceId = null, double strength = 1000) => ExecuteAsync(@"
        insert into cybernations_db.nation (id, nation_name, ruler_name, alliance_id, alliance_date, alliance_status,
            government_type, religion, team, created, technology, infrastructure, base_land, war_status, votes, strength,
            defcon, base_soldiers, tanks, cruise_missiles, nukes, recent_activity, connected_resource_1,
            connected_resource_2, connected_resource_3, connected_resource_4, connected_resource_5, connected_resource_6,
            connected_resource_7, connected_resource_8, connected_resource_9, connected_resource_10, resource_1, resource_2)
        values (@id, @nationName, @rulerName, @allianceId, now(), 'Member', 4, 1, 1, now(), 100, 200, 300, 2, 0, @strength,
            5, 0, 0, 0, 0, 1, '', '', '', '', '', '', '', '', '', '', '', '')",
        new { id, nationName, rulerName, allianceId, strength });

    /// <summary>
    /// Inserts an account. The account insert trigger generates its slots (4, plus 1 each for FM and DRA).
    /// </summary>
    public Task<int> AccountAsync(int nationId, char role = 'B', string psw = "ABCDE", string? discord = null,
        long discordId = 0, bool fm = false, bool fac = false, bool dra = false) => ScalarAsync<int>(@"
        insert into vep_db.account (nation_id, discord, discord_id, psw, va, fm, fac, dra, date_added)
        values (@nationId, @discord, @discordId, @psw, @role, @fm, @fac, @dra, now());
        select last_insert_id();",
        new { nationId, discord, discordId, psw, role = role.ToString(), fm, fac, dra });

    /// <summary>
    /// Inserts a nation (in the given alliance) and an account for it. The nation is named "{ruler}land".
    /// </summary>
    public async Task<int> MemberAsync(int nationId, string rulerName, char role = 'B', int? allianceId = null,
        string? discord = null, long discordId = 0, bool fm = false, bool dra = false, string psw = "ABCDE")
    {
        await NationAsync(nationId, $"{rulerName}land", rulerName, allianceId);
        return await AccountAsync(nationId, role, psw, discord ?? rulerName.ToLowerInvariant(), discordId, fm, dra: dra);
    }

    public async Task<int[]> SlotIdsAsync(int accountId) =>
        (await QueryAsync<int>("select id from vep_db.slot where account_id = @accountId order by slot_number", new { accountId })).ToArray();

    public Task<int> DealAsync(int slotA, int slotB, string status = "active", DateTime? createdOn = null, string createdBy = "system") => ScalarAsync<int>(@"
        insert into vep_db.deal (slot_1_id, slot_2_id, status, created_on, created_by, cancelled_on)
        values (@slot1, @slot2, @status, @createdOn, @createdBy, if(@status = 'cancelled', now(), null));
        select last_insert_id();",
        new { slot1 = Math.Min(slotA, slotB), slot2 = Math.Max(slotA, slotB), status, createdOn = createdOn ?? DateTime.UtcNow, createdBy });

    public Task DealStateAsync(int dealId, string state, int nextSender, int nextReceiver, int? responsible = null,
        DateTime? responsibleSince = null, string? blockedReason = null, int? inFlightAidId = null,
        DateTime? inFlightUntil = null, int? blockingAidId = null, DateTime? stateSince = null) => ExecuteAsync(@"
        insert into vep_db.deal_state (deal_id, state, state_since, blocked_reason, next_sender_account_id,
            next_receiver_account_id, in_flight_aid_id, in_flight_until, blocking_aid_id, responsible_account_id,
            responsible_since)
        values (@dealId, @state, @stateSince, @blockedReason, @nextSender, @nextReceiver, @inFlightAidId,
            @inFlightUntil, @blockingAidId, @responsible, @responsibleSince)",
        new
        {
            dealId, state, stateSince = stateSince ?? DateTime.UtcNow.Date, blockedReason, nextSender, nextReceiver,
            inFlightAidId, inFlightUntil, blockingAidId, responsible, responsibleSince
        });

    public Task<int> DealActivityAsync(int dealId, int legNumber, int senderNationId, int receiverNationId,
        DateTime activityDate, int? aidId = null, DateTime? approvedOn = null, bool linkedLate = false,
        string? failureReason = null) => ScalarAsync<int>(@"
        insert into vep_db.deal_activity (deal_id, aid_id, leg_number, expected_sender_nation_id,
            expected_receiver_nation_id, activity_date, approved_on, linked_late, failure_reason)
        values (@dealId, @aidId, @legNumber, @senderNationId, @receiverNationId, @activityDate, @approvedOn,
            @linkedLate, @failureReason);
        select last_insert_id();",
        new { dealId, aidId, legNumber, senderNationId, receiverNationId, activityDate, approvedOn, linkedLate, failureReason });

    public Task<int> DealFlagAsync(int dealId, string flagType, int flaggedAccountId, int innocentAccountId,
        string? flagReason = null, DateTime? flaggedOn = null, DateTime? resolvedOn = null) => ScalarAsync<int>(@"
        insert into vep_db.deal_flag (deal_id, flag_type, flag_reason, flagged_account_id, innocent_account_id,
            flagged_on, resolved_on)
        values (@dealId, @flagType, @flagReason, @flaggedAccountId, @innocentAccountId, @flaggedOn, @resolvedOn);
        select last_insert_id();",
        new { dealId, flagType, flagReason, flaggedAccountId, innocentAccountId, flaggedOn = flaggedOn ?? DateTime.UtcNow, resolvedOn });

    public Task AidAsync(int id, int senderNationId, int receiverNationId, DateTime date, int status = 2,
        int money = 0, int technology = 0, int soldiers = 0, string reason = "VEP") => ExecuteAsync(@"
        insert into cybernations_db.aid (id, sending_nation_id, sending_alliance_id, sending_team, receiving_nation_id,
            receiving_alliance_id, receiving_team, status, money, technology, soldiers, date, reason)
        values (@id, @senderNationId, 0, 1, @receiverNationId, 0, 1, @status, @money, @technology, @soldiers, @date, @reason)",
        new { id, senderNationId, receiverNationId, status, money, technology, soldiers, date, reason });

    public Task<int> RoleAsync(string name) => ScalarAsync<int>(
        "insert into vep_db.role (name) values (@name); select last_insert_id();", new { name });

    public Task AccountRoleAsync(int accountId, int roleId) => ExecuteAsync(
        "insert into vep_db.account_role (account_id, role_id) values (@accountId, @roleId)", new { accountId, roleId });

    public Task WaitQueueAsync(int accountId, int slotsNeeded, decimal priorityScore, DateTime? nextSlotOpensOn = null) => ExecuteAsync(@"
        insert into vep_db.wait_queue (account_id, slots_needed, next_slot_opens_on, priority_score)
        values (@accountId, @slotsNeeded, @nextSlotOpensOn, @priorityScore)",
        new { accountId, slotsNeeded, nextSlotOpensOn, priorityScore });
}
