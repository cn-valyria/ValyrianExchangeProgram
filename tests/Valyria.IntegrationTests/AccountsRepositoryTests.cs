using Valyria.Core.DTOs;
using Valyria.Core.Entities;
using Valyria.Infrastructure.Exceptions;
using Valyria.Infrastructure.Repositories;

namespace Valyria.IntegrationTests;

public class AccountsRepositoryTests(MySqlFixture fixture) : RepositoryTestBase(fixture)
{
    private AccountsRepository Repository => new(Fixture.VepConnectionString);

    [Fact]
    public async Task GetAccountDetails_ExcludesOnHoldAccountsUnlessAsked()
    {
        var active = await Data.MemberAsync(100, "Bob", 'B');
        var onHold = await Data.MemberAsync(200, "Sue", 'H');

        var withoutHold = await Repository.GetAccountDetailsAsync();
        var withHold = await Repository.GetAccountDetailsAsync(includeOnHold: true);

        Assert.Equal([active], withoutHold.Select(a => a.Id));
        Assert.Equal([active, onHold], withHold.Select(a => a.Id).Order());
    }

    [Fact]
    public async Task GetAccountDetails_MapsNationStatsSlotsAndOpenFlags()
    {
        await Data.AllianceAsync(1, "Valyria");
        var bob = await Data.MemberAsync(100, "Bob", 'B', allianceId: 1, discord: "bob#1", discordId: 111, fm: true, dra: true, psw: "QWERT");
        var sue = await Data.MemberAsync(200, "Sue", 'S');
        var deal = await Data.DealAsync((await Data.SlotIdsAsync(bob))[0], (await Data.SlotIdsAsync(sue))[0]);
        // Two flags on the same deal count once; resolved flags don't count
        await Data.DealFlagAsync(deal, "1_day_inactive", bob, sue);
        await Data.DealFlagAsync(deal, "1_day_inactive", sue, bob);
        await Data.DealFlagAsync(deal, "1_day_inactive", bob, sue, resolvedOn: DateTime.UtcNow);

        var account = (await Repository.GetAccountDetailsAsync()).Single(a => a.Id == bob);

        Assert.Equal(100, account.NationId);
        Assert.Equal("Bobland", account.NationName);
        Assert.Equal("Bob", account.RulerName);
        Assert.Equal("Valyria", account.AllianceName);
        Assert.Equal("bob#1", account.Discord);
        Assert.Equal(111, account.DiscordUniqueId);
        Assert.Equal("QWERT", account.UniqueCode);
        Assert.Equal('B', account.Role);
        Assert.Equal(RecentActivity.ActiveInTheLast3Days, account.Activity);
        Assert.Equal(NationalWarStatus.Peace, account.WarStatus);
        Assert.True(account.HasForeignMinistry);
        Assert.True(account.HasDisasterReliefAgency);
        Assert.False(account.HasFederalAidCommission);
        Assert.Equal(1, account.SlotsUsed);
        Assert.Equal(6, account.SlotsTotal);
        Assert.Equal(1, account.OpenFlagCount);
        Assert.False(account.NationDeleted);
    }

    [Fact]
    public async Task GetAccountDetails_ShowsDeletedNationsAsTheyWereWhenDeleted()
    {
        await Data.AllianceAsync(1, "Valyria");
        var bob = await Data.MemberAsync(100, "Bob", 'H', allianceId: 1);
        // No audit row to fall back on, e.g. a nation deleted before auditing started
        var sue = await Data.AccountAsync(200, 'H');

        // CN deleted Bob's nation. The nation delete trigger keeps its last state in audit_nation
        await Data.ExecuteAsync("delete from cybernations_db.nation where id = 100");

        var accounts = (await Repository.GetAccountDetailsAsync(includeOnHold: true)).ToDictionary(a => a.Id);

        Assert.Equal((100, "Bobland", "Bob", "Valyria", true),
            (accounts[bob].NationId, accounts[bob].NationName, accounts[bob].RulerName, accounts[bob].AllianceName, accounts[bob].NationDeleted));
        Assert.Equal(RecentActivity.ActiveInTheLast3Days, accounts[bob].Activity);
        Assert.Equal(1000m, accounts[bob].Strength);
        Assert.Equal((200, null, true), (accounts[sue].NationId, accounts[sue].NationName, accounts[sue].NationDeleted));
        Assert.True((await Repository.GetAccountByIdAsync(bob)).NationDeleted);
    }

    [Fact]
    public async Task GetAccountById_ReturnsTheAccount()
    {
        await Data.MemberAsync(100, "Bob");
        var sue = await Data.MemberAsync(200, "Sue", 'S');

        var account = await Repository.GetAccountByIdAsync(sue);

        Assert.Equal("Sue", account.RulerName);
        Assert.Equal(0, account.SlotsUsed);
        Assert.Equal(4, account.SlotsTotal);
    }

    [Fact]
    public async Task GetAccountById_ReturnsNull_ForAMissingAccount()
    {
        Assert.Null(await Repository.GetAccountByIdAsync(12345));
    }

    [Fact]
    public async Task CreateAccount_InsertsTheAccount_AndGeneratesItsSlots()
    {
        await Data.NationAsync(100, "Bobland", "Bob");

        var created = await Repository.CreateAccountAsync(new AccountCreateRequest
        {
            NationId = 100,
            Role = 'S',
            UniqueCode = "ZXCVB",
            Discord = "bob",
            DiscordUniqueId = 123456789,
            HasForeignMinistry = true
        });

        Assert.True(created.Id > 0);
        Assert.Equal("Bob", created.RulerName);
        Assert.Equal('S', created.Role);
        Assert.Equal(123456789, created.DiscordUniqueId);
        Assert.Equal(5, created.SlotsTotal);
        Assert.Equal(5, (await Data.SlotIdsAsync(created.Id)).Length);
    }

    [Fact]
    public async Task UpdateAccount_UpdatesEditableFields()
    {
        var bob = await Data.MemberAsync(100, "Bob", 'B');

        await Repository.UpdateAccountAsync(new AccountUpdateRequest
        {
            Id = bob,
            Role = 'H',
            UniqueCode = "NEWPW",
            Discord = "bob-new",
            DiscordUniqueId = 999,
            HasForeignMinistry = true,
            HasFederalAidCommission = true,
            HasDisasterReliefAgency = true
        });

        var account = await Repository.GetAccountByIdAsync(bob);
        Assert.Equal('H', account.Role);
        Assert.Equal("NEWPW", account.UniqueCode);
        Assert.Equal("bob-new", account.Discord);
        Assert.Equal(999, account.DiscordUniqueId);
        Assert.True(account.HasForeignMinistry);
        Assert.True(account.HasFederalAidCommission);
        Assert.True(account.HasDisasterReliefAgency);
    }

    [Fact]
    public async Task CreateAccount_AllowsBlankDiscordFields()
    {
        await Data.NationAsync(100, "Bobland", "Bob");

        var created = await Repository.CreateAccountAsync(new AccountCreateRequest
        {
            NationId = 100,
            Role = 'S',
            UniqueCode = "ZXCVB",
            Discord = null,
            DiscordUniqueId = null
        });

        Assert.Null(created.Discord);
        Assert.Null(created.DiscordUniqueId);
    }

    [Fact]
    public async Task UpdateAccount_AllowsClearingTheDiscordId()
    {
        var bob = await Data.MemberAsync(100, "Bob", 'B', discordId: 111);

        await Repository.UpdateAccountAsync(new AccountUpdateRequest
        {
            Id = bob,
            Role = 'B',
            UniqueCode = "ABCDE",
            DiscordUniqueId = null
        });

        Assert.Null((await Repository.GetAccountByIdAsync(bob)).DiscordUniqueId);
    }

    [Fact]
    public async Task RemoveAccount_DeletesTheAccount_AndKeepsAnAuditCopy()
    {
        var bob = await Data.MemberAsync(100, "Bob", 'B', psw: "ABCDE");

        await Repository.RemoveAccountAsync(bob);

        Assert.Equal(0, await Data.ScalarAsync<int>("select count(*) from account where id = @bob", new { bob }));
        Assert.Equal("ABCDE", await Data.ScalarAsync<string>("select psw from account_removed where nation_id = 100"));
    }

    [Fact]
    public async Task HoldAccount_CancelsItsActiveDeals_AndPutsItOnHold()
    {
        var bob = await Data.MemberAsync(100, "Bob", 'B');
        var sue = await Data.MemberAsync(200, "Sue", 'S');
        var bobSlots = await Data.SlotIdsAsync(bob);
        var sueSlots = await Data.SlotIdsAsync(sue);
        var active = await Data.DealAsync(bobSlots[0], sueSlots[0]);
        var alreadyCancelled = await Data.DealAsync(bobSlots[1], sueSlots[1], status: "cancelled");

        await Repository.HoldAccountAsync(bob, "admin");

        Assert.Equal('H', (await Repository.GetAccountByIdAsync(bob)).Role);
        Assert.Equal('S', (await Repository.GetAccountByIdAsync(sue)).Role);
        var dealSql = "select concat(status, '|', coalesce(cancelled_by, ''), '|', coalesce(cancellation_reason, '')) from deal where id = @id";
        Assert.Equal("cancelled|admin|Account put on hold", await Data.ScalarAsync<string>(dealSql, new { id = active }));
        Assert.Equal("cancelled||", await Data.ScalarAsync<string>(dealSql, new { id = alreadyCancelled }));
    }

    [Fact]
    public async Task UpdateAccountSettings_TurnsDiscordNotificationsOffAndOn()
    {
        var bob = await Data.MemberAsync(100, "Bob", 'B');
        var sue = await Data.MemberAsync(200, "Sue", 'S');
        Assert.True((await Repository.GetAccountByIdAsync(bob)).DiscordNotificationsEnabled);

        await Repository.UpdateAccountSettingsAsync(bob, new AccountSettings { DiscordNotificationsEnabled = false });

        Assert.False((await Repository.GetAccountByIdAsync(bob)).DiscordNotificationsEnabled);
        Assert.True((await Repository.GetAccountByIdAsync(sue)).DiscordNotificationsEnabled);

        await Repository.UpdateAccountSettingsAsync(bob, new AccountSettings { DiscordNotificationsEnabled = true });

        Assert.True((await Repository.GetAccountByIdAsync(bob)).DiscordNotificationsEnabled);
    }

    [Fact]
    public async Task LinkDiscordAccount_SetsTheDiscordIdAndUsername_ReplacingAnyBefore()
    {
        var bob = await Data.MemberAsync(100, "Bob", 'B', discord: "old#1", discordId: 111);
        var sue = await Data.MemberAsync(200, "Sue", 'S');

        await Repository.LinkDiscordAccountAsync(bob, 123456789012345678, "bob_");

        var account = await Repository.GetAccountByIdAsync(bob);
        Assert.Equal(123456789012345678, account.DiscordUniqueId);
        Assert.Equal("bob_", account.Discord);
        Assert.Null((await Repository.GetAccountByIdAsync(sue)).DiscordUniqueId);
        Assert.Equal([100], (await Repository.GetAccountsByDiscordIdAsync(123456789012345678)).Select(a => a.NationId));
    }

    [Fact]
    public async Task UpdateAndRemove_ThrowNotFound_ForAMissingAccount()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Repository.HoldAccountAsync(12345, "admin"));
        await Assert.ThrowsAsync<NotFoundException>(() => Repository.UpdateAccountSettingsAsync(12345, new AccountSettings()));
        await Assert.ThrowsAsync<NotFoundException>(() => Repository.LinkDiscordAccountAsync(12345, 1, "bob_"));
        await Assert.ThrowsAsync<NotFoundException>(() => Repository.UpdateAccountAsync(new AccountUpdateRequest { Id = 12345 }));
        await Assert.ThrowsAsync<NotFoundException>(() => Repository.RemoveAccountAsync(12345));
    }

    [Fact]
    public async Task FindProspectAccount_ReturnsANationWithoutAnAccount()
    {
        await Data.AllianceAsync(1, "Valyria");
        await Data.NationAsync(100, "Bobland", "Bob", allianceId: 1);

        var prospect = await Repository.FindProspectAccountAsync(100);

        Assert.Equal(100, prospect.NationId);
        Assert.Equal("Bobland", prospect.NationName);
        Assert.Equal("Bob", prospect.RulerName);
        Assert.Equal("Valyria", prospect.AllianceName);
    }

    [Fact]
    public async Task FindProspectAccount_ReturnsNull_ForAnUnknownNation()
    {
        Assert.Null(await Repository.FindProspectAccountAsync(404));
    }

    [Fact]
    public async Task FindProspectAccount_Throws_WhenTheNationAlreadyHasAnAccount()
    {
        await Data.MemberAsync(100, "Bob");

        var exception = await Assert.ThrowsAsync<AlreadyExistsException>(() => Repository.FindProspectAccountAsync(100));
        Assert.Contains("Bobland", exception.Message);
    }

    /// <summary>
    /// Seeds an account with a deal against its own partner (which never sends), and the legs it sent in the last
    /// 80 CN days. Returns the account and its deal
    /// </summary>
    private async Task<(int Account, int Deal)> SenderAsync(int nationId, string rulerName, int legs, char role = 'B')
    {
        var cnToday = await Data.CnTodayAsync();
        var account = await Data.MemberAsync(nationId, rulerName, role, allianceId: 1);
        var partner = await Data.MemberAsync(nationId + 1, $"{rulerName}Partner", allianceId: 1);
        var deal = await Data.DealAsync((await Data.SlotIdsAsync(account))[0], (await Data.SlotIdsAsync(partner))[0]);
        for (var leg = 1; leg <= legs; leg++)
            await Data.DealActivityAsync(deal, leg, nationId, nationId + 1, cnToday.AddDays(-(leg % 80)));
        return (account, deal);
    }

    private Task<int> FlagAsync(int deal, int account, DateTime flaggedOn, bool excused = false) => Data.ScalarAsync<int>(@"
        insert into vep_db.deal_flag (deal_id, flag_type, flag_reason, flagged_account_id, innocent_account_id, flagged_on, excused)
        select @deal, '1_day_inactive', 'awaiting_send', @account, if(account_1_id = @account, account_2_id, account_1_id), @flaggedOn, @excused
        from vep_db.deal_participants where deal_id = @deal;
        select last_insert_id();",
        new { deal, account, flaggedOn, excused });

    [Fact]
    public async Task GetLeaderboard_RanksByUnexcusedFlagsPerOutgoingLeg_OverTheLast90Days()
    {
        await Data.AllianceAsync(1, "Valyria");
        var cnToday = await Data.CnTodayAsync();
        var (ace, aceDeal) = await SenderAsync(100, "Ace", 6);
        var (bea, beaDeal) = await SenderAsync(200, "Bea", 4);
        var (cal, calDeal) = await SenderAsync(300, "Cal", 10);
        var (dee, deeDeal) = await SenderAsync(400, "Dee", 5);
        var (eve, eveDeal) = await SenderAsync(500, "Eve", 10);
        var (fay, fayDeal) = await SenderAsync(600, "Fay", 5);
        var (gus, _) = await SenderAsync(700, "Gus", 4);
        var (held, heldDeal) = await SenderAsync(800, "Held", 6, role: 'H');

        // A failed offer only counts as a leg once it's excused, and legs before the window don't count
        await Data.DealActivityAsync(aceDeal, 7, 100, 101, cnToday, failureReason: "expired", failedOn: cnToday);
        var excusedLeg = await Data.DealActivityAsync(beaDeal, 5, 200, 201, cnToday, failureReason: "expired", failedOn: cnToday);
        await Data.ExecuteAsync("update vep_db.deal_activity set failure_excused = true where id = @excusedLeg", new { excusedLeg });
        await Data.DealActivityAsync(calDeal, 11, 300, 301, cnToday.AddDays(-90));
        await Data.DealActivityAsync(calDeal, 12, 300, 301, cnToday.AddDays(-89));

        // Excused flags and flags before the window don't count
        await FlagAsync(calDeal, cal, DateTime.UtcNow.AddDays(-10));
        await FlagAsync(calDeal, cal, DateTime.UtcNow.AddDays(-5), excused: true);
        await FlagAsync(calDeal, cal, DateTime.UtcNow.AddDays(-91));
        await FlagAsync(eveDeal, eve, DateTime.UtcNow.AddDays(-30));
        await FlagAsync(eveDeal, eve, DateTime.UtcNow.AddDays(-20));
        await FlagAsync(deeDeal, dee, DateTime.UtcNow);
        await FlagAsync(fayDeal, fay, DateTime.UtcNow);
        await FlagAsync(heldDeal, held, DateTime.UtcNow);
        // Flags raised against the partner, who never sends, don't put it on the leaderboard
        await FlagAsync(aceDeal, ace + 1, DateTime.UtcNow);

        var leaderboard = await Repository.GetLeaderboardAsync(gus);

        Assert.Equal((90, 5, cnToday.AddDays(-89), 6), (leaderboard.WindowDays, leaderboard.MinimumOutgoingLegs,
            leaderboard.WindowStart, leaderboard.RankedAccounts));
        Assert.Equal(
            [("Ace", 1, 0, 6), ("Bea", 2, 0, 5), ("Cal", 3, 1, 11), ("Eve", 4, 2, 10), ("Dee", 5, 1, 5), ("Fay", 5, 1, 5)],
            leaderboard.Top.Select(s => (s.RulerName, s.Rank!.Value, s.Flags, s.OutgoingLegs)));
        var calStanding = leaderboard.Top[2];
        Assert.Equal((0.0909m, cnToday.AddDays(80), "Valyria", 300), (calStanding.FlagsPerLeg, calStanding.NextFlagDropsOn,
            calStanding.AllianceName, calStanding.NationId));
        Assert.Null(leaderboard.Top[0].NextFlagDropsOn);

        // Gus hasn't sent enough legs to be ranked, but still sees his own numbers
        Assert.Equal((gus, null, 1, 4), (leaderboard.Mine!.AccountId, leaderboard.Mine.Rank, leaderboard.Mine.LegsToQualify,
            leaderboard.Mine.OutgoingLegs));
        Assert.Equal(cal, (await Repository.GetLeaderboardAsync(cal)).Mine!.AccountId);
        Assert.Equal((0, 5), ((await Repository.GetLeaderboardAsync(ace + 1)).Mine!.OutgoingLegs,
            (await Repository.GetLeaderboardAsync(ace + 1)).Mine!.LegsToQualify));
        Assert.Null((await Repository.GetLeaderboardAsync(held)).Mine);
    }

    [Fact]
    public async Task GetLeaderboard_ShowsTheTop10Ranks()
    {
        await Data.AllianceAsync(1, "Valyria");
        var accounts = new List<(int Account, int Deal)>();
        for (var i = 0; i < 12; i++)
            accounts.Add(await SenderAsync(100 + i * 10, $"Sender{i:00}", 5));
        for (var i = 0; i < 12; i++)
            for (var flag = 0; flag < i; flag++)
                await FlagAsync(accounts[i].Deal, accounts[i].Account, DateTime.UtcNow);

        var leaderboard = await Repository.GetLeaderboardAsync(accounts[11].Account);

        Assert.Equal(12, leaderboard.RankedAccounts);
        Assert.Equal(Enumerable.Range(1, 10), leaderboard.Top.Select(s => s.Rank!.Value));
        Assert.Equal((12, 11), (leaderboard.Mine!.Rank, leaderboard.Mine.Flags));
    }

    [Fact]
    public async Task GetAccountsByDiscordId_ReturnsEveryAccountLinkedToTheDiscordUser()
    {
        // Discord IDs are snowflakes above 2^53, so these two only differ past a double's precision
        const long discordId = 123456789012345678;
        await Data.MemberAsync(200, "Bob", discordId: discordId, psw: "QWERT");
        await Data.MemberAsync(100, "Bob", discordId: discordId, psw: "ABCDE");
        await Data.MemberAsync(300, "Sue", discordId: discordId + 1, psw: "ZXCVB");
        await Data.AccountAsync(400, psw: "POIUY", discordId: discordId);

        var accounts = (await Repository.GetAccountsByDiscordIdAsync(discordId)).ToList();

        Assert.Equal([100, 200, 400], accounts.Select(a => a.NationId));
        Assert.Equal(["ABCDE", "QWERT", "POIUY"], accounts.Select(a => a.UniqueCode));
        Assert.Equal(("Bobland", "Bob"), (accounts[0].NationName, accounts[0].RulerName));
        // No nation row (e.g. CN deleted it)
        Assert.Null(accounts[2].NationName);
    }

    [Fact]
    public async Task GetAccountsByDiscordId_ReturnsNothing_ForAnUnregisteredDiscordUser()
    {
        await Data.MemberAsync(100, "Bob", discordId: 111);

        Assert.Empty(await Repository.GetAccountsByDiscordIdAsync(222));
    }
}
