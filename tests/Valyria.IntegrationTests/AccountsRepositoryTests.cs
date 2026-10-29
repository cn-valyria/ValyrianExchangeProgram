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
    public async Task UpdateAndRemove_ThrowNotFound_ForAMissingAccount()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Repository.HoldAccountAsync(12345, "admin"));
        await Assert.ThrowsAsync<NotFoundException>(() => Repository.UpdateAccountSettingsAsync(12345, new AccountSettings()));
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

    [Fact]
    public async Task GetTopScorers_RanksBySpeedOfClearingOutgoingLists()
    {
        await Data.AllianceAsync(1, "Valyria");
        var fast = await Data.MemberAsync(100, "Fast", allianceId: 1);
        var slow = await Data.MemberAsync(200, "Slow", allianceId: 1);
        var onHold = await Data.MemberAsync(300, "Held", 'H', allianceId: 1);
        var incomingOnly = await Data.MemberAsync(400, "Incoming", allianceId: 1);
        await Data.ExecuteAsync(@"
            insert into account_list_performance (account_id, list_id, day_started, days_until_removed) values
                (@fast, 1, '2026-01-01', 1), (@fast, 3, '2026-01-05', 2),
                (@slow, 1, '2026-01-01', 6),
                (@onHold, 1, '2026-01-01', 0),
                (@incomingOnly, 2, '2026-01-01', 0)",
            new { fast, slow, onHold, incomingOnly });

        var scorers = (await Repository.GetTopScorerAccountsAsync()).ToList();

        Assert.Equal(["Fast", "Slow"], scorers.Select(s => s.RulerName));
        Assert.Equal([1, 2], scorers.Select(s => s.Rank));
        Assert.Equal(2, scorers[0].TotalTimesInDebt);
        Assert.Equal(1.5m, scorers[0].AvgDaysToClearDebt);
        Assert.Equal("Valyria", scorers[0].AllianceName);
    }
}
