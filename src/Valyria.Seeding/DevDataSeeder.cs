using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Valyria.Core.Interfaces;

namespace Valyria.Seeding;

/// <summary>
/// Fills an empty local vep_db with a small VEP program built on the real CN nations and aid the data loader just
/// imported, so every date lines up with today.
/// </summary>
/// <remarks>
/// Deals with a real aid offer between the pair are created just before that offer, so the real procedures link it as
/// the first leg. The rest are between nations with free slots, backdated to the state under test, and then the real
/// procedures work out each deal's state, flags and cancellations, and match every slot that's still open.
/// </remarks>
public sealed partial class DevDataSeeder
{
    /// <summary>
    /// Every seeded account logs in with this unique code
    /// </summary>
    public const string UniqueCode = "LOCAL";

    private const string SuperAdmin = "Super Admin";
    private const string ReadOnlyAdmin = "Admin (Read-only)";
    private const string ListsAdmin = "Admin - Lists";

    private readonly SeedData _data;
    private readonly IDataLoaderRepository _dataLoaderRepository;
    private readonly ILogger<DevDataSeeder> _logger;
    private readonly long? _devDiscordId;

    private readonly HashSet<int> _usedNations = [];
    private readonly List<SeededAccount> _accounts = [];

    public DevDataSeeder(string vepConnectionString, IDataLoaderRepository dataLoaderRepository,
        ILogger<DevDataSeeder> logger, long? devDiscordId)
    {
        _data = new SeedData(vepConnectionString);
        _dataLoaderRepository = dataLoaderRepository;
        _logger = logger;
        _devDiscordId = devDiscordId;
    }

    /// <summary>
    /// Real aid scenarios: (role of the sender, role of the receiver, cash or tech, aid status, how many)
    /// </summary>
    private static readonly (char Sender, char Receiver, bool Cash, int Status, int Count)[] RealAidDeals =
    [
        ('B', 'S', true, 2, 3),  // in flight, accepted
        ('B', 'S', true, 1, 2),  // in flight, waiting for the receiver to accept (accept_aid mass message)
        ('F', 'R', false, 2, 1),
        ('F', 'R', false, 1, 1),
        ('D', 'C', true, 2, 1),
    ];

    /// <summary>
    /// Deals between nations with free slots, by how many days the expected sender has been holding them up.
    /// 0: awaiting send today. 2: 1-day warning (overdue mass message). 4: final warning. 6: cancelled with a 5-day
    /// replacement flag
    /// </summary>
    private static readonly (string Name, char Sender, char Receiver, int DaysHeldUp)[] AwaitingSendDeals =
    [
        ("awaiting send", 'B', 'S', 0),
        ("awaiting send", 'B', 'S', 0),
        ("1-day warning", 'B', 'S', 2),
        ("1-day warning, extended", 'B', 'S', 2),
        ("final warning", 'B', 'S', 4),
        ("cancelled after 5 days", 'D', 'C', 6),
    ];

    public async Task<bool> SeedAsync()
    {
        if (await _data.ScalarAsync<int>("select count(*) from vep_db.account") > 0)
        {
            _logger.LogInformation("vep_db already has accounts, so it's been seeded. Use the dashboard's Reset & reseed command to start over");
            return false;
        }

        if (await _data.ScalarAsync<int>("select count(*) from cybernations_db.nation") == 0)
            throw new InvalidOperationException("No CN nations are loaded. The data loader has to import today's files before seeding");

        var today = await _data.CnTodayAsync();
        _logger.LogInformation("Seeding vep_db for CN date {today:yyyy-MM-dd}", today);

        var roles = new Dictionary<string, int>
        {
            [SuperAdmin] = await _data.RoleAsync(SuperAdmin),
            [ReadOnlyAdmin] = await _data.RoleAsync(ReadOnlyAdmin),
            [ListsAdmin] = await _data.RoleAsync(ListsAdmin),
        };

        var activeAid = (await _data.QueryAsync<ActiveAid>(@"
            select aid.id as Id, aid.sending_nation_id as SenderNationId, aid.receiving_nation_id as ReceiverNationId,
                aid.status as Status, aid.money as Money, aid.technology as Technology, aid.date as Date
            from cybernations_db.aid
            join cybernations_db.nation s on s.id = aid.sending_nation_id
            join cybernations_db.nation r on r.id = aid.receiving_nation_id
            where aid.status in (1, 2)
                and date(aid.date) > @today - interval 10 day
            order by aid.date desc, aid.id desc", new { today })).ToList();

        // Aid slots each nation is using, the same way update_deal_state counts them
        var slotsUsed = activeAid
            .SelectMany(aid => new[] { aid.SenderNationId, aid.ReceiverNationId })
            .GroupBy(id => id)
            .ToDictionary(group => group.Key, group => group.Count());

        // Bigger, allied nations first, so the seeded program looks like the real one
        var nations = (await _data.QueryAsync<Nation>(@"
            select id as Id, ruler_name as RulerName, team as Team
            from cybernations_db.nation
            where alliance_id is not null and alliance_id <> 0
            order by strength desc, id")).ToList();
        var idleNations = new Queue<Nation>(nations.Where(nation => !slotsUsed.ContainsKey(nation.Id)));
        var busyNations = new Queue<Nation>(nations.Where(nation => slotsUsed.GetValueOrDefault(nation.Id) >= 4));
        var nationsById = nations.ToDictionary(nation => nation.Id);

        // Deals on real aid
        foreach (var (sender, receiver, cash, status, count) in RealAidDeals)
        {
            var offers = activeAid
                .Where(aid => aid.Status == status && (cash ? aid.Money > 0 : aid.Money == 0 && aid.Technology > 0))
                .Where(aid => aid.SenderNationId != aid.ReceiverNationId
                    && nationsById.ContainsKey(aid.SenderNationId) && nationsById.ContainsKey(aid.ReceiverNationId)
                    && !_usedNations.Contains(aid.SenderNationId) && !_usedNations.Contains(aid.ReceiverNationId))
                .DistinctBy(aid => (aid.SenderNationId, aid.ReceiverNationId))
                .ToList();

            foreach (var aid in offers.Take(count))
            {
                if (_usedNations.Contains(aid.SenderNationId) || _usedNations.Contains(aid.ReceiverNationId))
                    continue;

                var senderAccount = await AccountAsync(nationsById[aid.SenderNationId], sender, "in flight");
                var receiverAccount = await AccountAsync(nationsById[aid.ReceiverNationId], receiver, "in flight");

                // A leg only counts if it's dated on or after the deal was created
                await DealAsync(senderAccount, receiverAccount, slot: 1, createdOn: aid.Date.Date.AddDays(-1));
            }
        }

        // Deals waiting on the sender
        var heldUpDeals = new List<(int DealId, int DaysHeldUp, string Name)>();
        foreach (var (name, sender, receiver, daysHeldUp) in AwaitingSendDeals)
        {
            var senderAccount = await AccountAsync(idleNations.Dequeue(), sender, name);
            var receiverAccount = await AccountAsync(idleNations.Dequeue(), receiver, name);
            var dealId = await DealAsync(senderAccount, receiverAccount, slot: 1, createdOn: today.AddDays(-daysHeldUp - 1));
            heldUpDeals.Add((dealId, daysHeldUp, name));
        }

        // A sender whose slots are all taken by other aid: never activated (pending start, nobody's fault), and
        // activated before its slots filled up (blocked, the sender's fault)
        var pendingStartDeal = await DealAsync(
            await AccountAsync(busyNations.Dequeue(), 'B', "pending start"),
            await AccountAsync(idleNations.Dequeue(), 'S', "pending start"),
            slot: 1, createdOn: today.AddDays(-1));
        var blockedDeal = await DealAsync(
            await AccountAsync(busyNations.Dequeue(), 'B', "blocked, slots full"),
            await AccountAsync(idleNations.Dequeue(), 'S', "blocked, slots full"),
            slot: 1, createdOn: today.AddDays(-4));

        // On hold: never matched
        await AccountAsync(idleNations.Dequeue(), 'H', "on hold");
        await AccountAsync(idleNations.Dequeue(), 'H', "on hold");

        // Admins, the account options and a Discord opt-out, spread over accounts that already have a scenario
        var superAdmin = _accounts.First(account => account.Role == 'B');
        await _data.AccountRoleAsync(superAdmin.AccountId, roles[SuperAdmin]);
        await _data.AccountRoleAsync(_accounts.First(account => account.Role == 'S').AccountId, roles[ReadOnlyAdmin]);
        await _data.AccountRoleAsync(_accounts.First(account => account.Scenario == "1-day warning").AccountId, roles[ListsAdmin]);
        superAdmin.AdminRole = SuperAdmin;
        _accounts.First(account => account.Role == 'S').AdminRole = ReadOnlyAdmin;
        _accounts.First(account => account.Scenario == "1-day warning").AdminRole = ListsAdmin;

        await SetAccountOptionAsync(_accounts.FirstOrDefault(account => account.Role == 'S'), "fm = 1");
        await SetAccountOptionAsync(_accounts.FirstOrDefault(account => account.Role == 'F'), "dra = 1");
        await SetAccountOptionAsync(_accounts.FirstOrDefault(account => account.Role == 'R'), "fac = 1");
        await SetAccountOptionAsync(_accounts.FirstOrDefault(account => account.Scenario == "final warning" && account.Role == 'B'),
            "discord_notifications_enabled = false");

        // Pings only go to accounts with a Discord ID, so only the developer's own account gets one
        if (_devDiscordId is { } devDiscordId)
        {
            await _data.ExecuteAsync("update vep_db.account set discord_id = @devDiscordId where id = @accountId",
                new { devDiscordId, accountId = superAdmin.AccountId });
            superAdmin.Notes = "your Discord ID";
        }

        // A +1 aid slot event for the team with the most seeded members, so they get a bonus slot
        var eventTeam = _accounts.Where(account => account.Role != 'H').GroupBy(account => account.Team)
            .OrderByDescending(group => group.Count()).First().Key;
        await _data.TeamEventAsync(eventTeam, today.AddDays(-5), createdBy: superAdmin.RulerName);

        // Link the real aid as legs and work out every deal's state
        await _dataLoaderRepository.UpdateDealStateAsync(checkInactivity: false);

        // Backdate how long each sender has been holding up its deal
        foreach (var (dealId, daysHeldUp, _) in heldUpDeals.Where(deal => deal.DaysHeldUp > 0))
            await _data.ExecuteAsync(@"
                update vep_db.deal_state
                set responsible_since = @since, state_since = @since, activated_on = @since
                where deal_id = @dealId", new { dealId, since = today.AddDays(-daysHeldUp) });

        // The blocked deal had started before the sender filled its slots
        await _data.ExecuteAsync("update vep_db.deal_state set activated_on = @since where deal_id = @blockedDeal",
            new { blockedDeal, since = today.AddDays(-3) });
        await _dataLoaderRepository.UpdateDealStateAsync(checkInactivity: false);
        await _data.ExecuteAsync(@"
            update vep_db.deal_state
            set responsible_since = @since, state_since = @since
            where deal_id = @blockedDeal and state = 'blocked'", new { blockedDeal, since = today.AddDays(-2) });

        // Flag the senders holding up deals, and cancel the one past 5 days
        await _dataLoaderRepository.UpdateDealStateAsync(checkInactivity: true);

        // An admin gave one warned sender 5 more days, moving the deadline 5 days past when it would have cancelled
        var (extendedDeal, extendedDaysHeldUp, _) = heldUpDeals.First(deal => deal.Name == "1-day warning, extended");
        await _data.ExecuteAsync(@"
            insert into vep_db.deal_extension (deal_id, flag_id, account_id, kind, extended_until, legs_counted_at_grant,
                granted_by, admin_notes)
            select df.deal_id, df.id, df.flagged_account_id, 'five_days', @extendedUntil, 0, @grantedBy,
                'Seeded: on vacation until the weekend'
            from vep_db.deal_flag df
            where df.deal_id = @extendedDeal and df.flag_type = '1_day_inactive' and df.resolved_on is null",
            new { extendedDeal, extendedUntil = today.AddDays(-extendedDaysHeldUp + 5 + 5), grantedBy = superAdmin.RulerName });

        // A couple of older cancellations for the admin dashboard, on the awaiting send accounts' second slots
        var awaitingSend = _accounts.Where(account => account.Scenario == "awaiting send").ToList();
        await DealAsync(awaitingSend[0], awaitingSend[3], slot: 2, createdOn: today.AddDays(-20), cancelledOn: today.AddDays(-8),
            cancellationReason: "Partner left the program");
        await DealAsync(awaitingSend[2], awaitingSend[1], slot: 2, createdOn: today.AddDays(-15), cancelledOn: today.AddDays(-3),
            cancellationReason: "Cancelled by admin at the members' request");

        // Fill every slot that's still open, the same way the daily run does
        var matched = await _dataLoaderRepository.MatchDealsAsync();

        var summary = await _data.QueryAsync<DealCount>(@"
            select coalesce(ds.state, d.status) as State, count(*) as Deals
            from vep_db.deal d
            left join vep_db.deal_state ds on ds.deal_id = d.id
            group by coalesce(ds.state, d.status)
            order by State");
        var flags = await _data.ScalarAsync<int>("select count(*) from vep_db.deal_flag where resolved_on is null");

        _logger.LogInformation("Seeded {accounts} accounts. Matching added {matched} deals. Deals by state: {states}. {flags} unresolved flags",
            _accounts.Count, matched.DealsCreated, string.Join(", ", summary.Select(row => $"{row.State} {row.Deals}")), flags);
        _logger.LogInformation("Log in with any of these ruler names (or nation IDs) and the unique code {code}:\n{accounts}",
            UniqueCode, string.Join('\n', _accounts.Select(account => account.ToString())));

        return true;
    }

    private async Task<SeededAccount> AccountAsync(Nation nation, char role, string scenario)
    {
        _usedNations.Add(nation.Id);

        var accountId = await _data.AccountAsync(nation.Id, role, UniqueCode, DiscordName(nation.RulerName));
        var account = new SeededAccount(accountId, nation.Id, nation.RulerName, nation.Team, role, scenario);
        _accounts.Add(account);
        return account;
    }

    // Some real aid scenarios can come up empty on a quiet CN day, so a role might not have been seeded
    private Task SetAccountOptionAsync(SeededAccount? account, string assignment)
    {
        if (account is null)
            return Task.CompletedTask;

        account.Notes = account.Notes is null ? assignment : $"{account.Notes}, {assignment}";
        return _data.ExecuteAsync($"update vep_db.account set {assignment} where id = @accountId", new { accountId = account.AccountId });
    }

    private async Task<int> DealAsync(SeededAccount first, SeededAccount second, int slot, DateTime createdOn,
        DateTime? cancelledOn = null, string? cancellationReason = null)
    {
        var slotA = (await _data.SlotIdsAsync(first.AccountId))[slot - 1];
        var slotB = (await _data.SlotIdsAsync(second.AccountId))[slot - 1];
        var dealId = await _data.DealAsync(slotA, slotB, cancelledOn is null ? "active" : "cancelled", createdOn);

        if (cancelledOn is not null)
            await _data.ExecuteAsync(@"
                update vep_db.deal
                set cancelled_on = @cancelledOn, cancelled_by = 'system', cancellation_reason = @cancellationReason
                where id = @dealId", new { dealId, cancelledOn, cancellationReason });

        return dealId;
    }

    // vep_db.account is latin1, and ruler names can be anything
    private static string DiscordName(string rulerName)
    {
        var name = DiscordNameCharacters().Replace(rulerName.ToLowerInvariant(), string.Empty);
        return name.Length == 0 ? "member" : name[..Math.Min(name.Length, 32)];
    }

    [GeneratedRegex("[^a-z0-9_.]")]
    private static partial Regex DiscordNameCharacters();

    private sealed class ActiveAid
    {
        public int Id { get; init; }
        public int SenderNationId { get; init; }
        public int ReceiverNationId { get; init; }
        public int Status { get; init; }
        public int Money { get; init; }
        public int Technology { get; init; }
        public DateTime Date { get; init; }
    }

    private sealed class Nation
    {
        public int Id { get; init; }
        public string RulerName { get; init; } = string.Empty;
        public int Team { get; init; }
    }

    private sealed class DealCount
    {
        public string State { get; init; } = string.Empty;
        public long Deals { get; init; }
    }

    private sealed class SeededAccount(int accountId, int nationId, string rulerName, int team, char role, string scenario)
    {
        public int AccountId { get; } = accountId;
        public int NationId { get; } = nationId;
        public string RulerName { get; } = rulerName;
        public int Team { get; } = team;
        public char Role { get; } = role;
        public string Scenario { get; } = scenario;
        public string? AdminRole { get; set; }
        public string? Notes { get; set; }

        public override string ToString() =>
            $"  {RulerName} (nation {NationId}): role {Role}, {Scenario}" +
            (AdminRole is null ? string.Empty : $", {AdminRole}") +
            (Notes is null ? string.Empty : $", {Notes}");
    }
}
