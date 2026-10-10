using MySql.Data.MySqlClient;
using Valyria.Infrastructure.Repositories;

namespace Valyria.IntegrationTests;

public class AdminRepositoryTests(MySqlFixture fixture) : RepositoryTestBase(fixture)
{
    private AdminRepository Repository => new(Fixture.VepConnectionString);

    /// <summary>
    /// A buyer and seller in Valyria with an active deal, a donor in Rogues, and an on-hold seller who is ignored everywhere
    /// </summary>
    private async Task<(int Buyer, int Seller, int Donor, int OnHold)> SeedProgramAsync()
    {
        await Data.AllianceAsync(1, "Valyria");
        await Data.AllianceAsync(2, "Rogues");
        var buyer = await Data.MemberAsync(100, "Bob", 'B', allianceId: 1);
        var seller = await Data.MemberAsync(200, "Sue", 'S', allianceId: 1, fm: true);
        var donor = await Data.MemberAsync(300, "Dan", 'D', allianceId: 2);
        var onHold = await Data.MemberAsync(400, "Hal", 'H', allianceId: 2);
        await Data.DealAsync((await Data.SlotIdsAsync(buyer))[0], (await Data.SlotIdsAsync(seller))[0]);
        await Data.DealAsync((await Data.SlotIdsAsync(buyer))[1], (await Data.SlotIdsAsync(seller))[1], status: "cancelled");
        return (buyer, seller, donor, onHold);
    }

    [Fact]
    public async Task GeneralProgramSummary_CountsActiveAccountsAndDeals()
    {
        await SeedProgramAsync();

        var summary = await Repository.GetGeneralProgramSummaryAsync();

        Assert.Equal(3, summary.TotalAccounts);
        Assert.Equal(1, summary.ActiveDeals);
    }

    [Fact]
    public async Task AidRatioSummary_CountsSlotsByAidDirection()
    {
        await SeedProgramAsync();

        var summary = await Repository.GetAidRatioSummaryAsync();

        // Buyer: 4 slots receive tech and send cash. Seller (FM): 5 slots send tech and receive cash. Donor: 4 slots send cash
        Assert.Equal(5, summary.TotalIncomingCashSlots);
        Assert.Equal(8, summary.TotalOutgoingCashSlots);
        Assert.Equal(4, summary.TotalIncomingTechSlots);
        Assert.Equal(5, summary.TotalOutgoingTechSlots);
    }

    [Fact]
    public async Task AidRatioSummary_IsZero_WithNoAccounts()
    {
        var summary = await Repository.GetAidRatioSummaryAsync();

        Assert.Equal(0, summary.TotalIncomingCashSlots + summary.TotalOutgoingCashSlots + summary.TotalIncomingTechSlots + summary.TotalOutgoingTechSlots);
    }

    [Fact]
    public async Task DealHealthSummary_CountsTheWaitQueueAndOpenFlags()
    {
        var (buyer, seller, donor, _) = await SeedProgramAsync();
        await Data.WaitQueueAsync(buyer, 3, 10);
        await Data.WaitQueueAsync(donor, 4, 5);
        var deal = await Data.ScalarAsync<int>("select id from deal where status = 'active'");
        await Data.DealFlagAsync(deal, "1_day_inactive", buyer, seller);
        await Data.DealFlagAsync(deal, "1_day_inactive", seller, buyer, resolvedOn: DateTime.UtcNow);

        var summary = await Repository.GetDealHealthSummaryAsync();

        Assert.Equal(2, summary.AccountsWaiting);
        Assert.Equal(7, summary.SlotsWaiting);
        Assert.Equal(1, summary.OpenFlags);
    }

    [Fact]
    public async Task SlotUsageSummary_TotalsCapacityAndUse()
    {
        await SeedProgramAsync();

        var summary = await Repository.GetSlotUsageSummaryAsync();

        Assert.Equal(4 + 5 + 4, summary.MaxCapacity);
        Assert.Equal(2, summary.CurrentSlotsUsed);
    }

    [Fact]
    public async Task DealAidPerDay_CountsTheLastFiveDays()
    {
        await SeedProgramAsync();
        var deal = await Data.ScalarAsync<int>("select id from deal where status = 'active'");
        var today = await Data.ScalarAsync<DateTime>("select curdate()");
        await Data.DealActivityAsync(deal, 1, 100, 200, today.AddDays(-1));
        await Data.DealActivityAsync(deal, 2, 200, 100, today.AddDays(-1));
        await Data.DealActivityAsync(deal, 3, 100, 200, today);
        await Data.DealActivityAsync(deal, 4, 200, 100, today.AddDays(-5));

        var perDay = await Repository.GetDealAidPerDayAsync();

        Assert.Equal([today.AddDays(-1), today], perDay.Select(d => d.Day));
        Assert.Equal([2, 1], perDay.Select(d => d.AidCount));
    }

    [Fact]
    public async Task TotalSlotsPerAlliance_GroupsNonZeroSlotsByAidDirection()
    {
        await SeedProgramAsync();

        var groups = (await Repository.GetTotalSlotsPerAllianceAsync()).ToDictionary(g => g.SlotGroupName, g => g.TotalSlotGroups);

        Assert.Equal(["Incoming Cash", "Outgoing Cash", "Incoming Tech", "Outgoing Tech"], groups.Keys);
        Assert.Equal([("Valyria", 5)], groups["Incoming Cash"].Select(g => (g.AllianceName, g.TotalSlots)));
        Assert.Equal([("Rogues", 4), ("Valyria", 4)], groups["Outgoing Cash"].Select(g => (g.AllianceName, g.TotalSlots)).Order());
        Assert.Equal([("Valyria", 4)], groups["Incoming Tech"].Select(g => (g.AllianceName, g.TotalSlots)));
        Assert.Equal([("Valyria", 5)], groups["Outgoing Tech"].Select(g => (g.AllianceName, g.TotalSlots)));
    }

    [Fact]
    public async Task AccountsPerRole_CountsActiveAccounts()
    {
        await SeedProgramAsync();

        var perRole = (await Repository.GetAccountsPerRoleAsync()).ToDictionary(r => r.Role, r => r.TotalAccounts);

        Assert.Equal(new Dictionary<char, int> { ['B'] = 1, ['S'] = 1, ['D'] = 1 }, perRole);
    }

    [Fact]
    public async Task OpenSlotsPerRole_CountsUnusedSlots()
    {
        await SeedProgramAsync();

        var perRole = await Repository.GetOpenSlotsPerRoleAsync();

        Assert.Equal([('B', 3), ('D', 4), ('S', 4)], perRole.Select(r => (r.Role, r.OpenSlots)));
    }

    [Fact]
    public async Task AccountsPerAlliance_CountsActiveAccounts_LargestFirst()
    {
        await SeedProgramAsync();

        var perAlliance = await Repository.GetAccountsPerAllianceAsync();

        Assert.Equal([("Valyria", 2), ("Rogues", 1)], perAlliance.Select(a => (a.AllianceName, a.TotalAccounts)));
    }

    [Fact]
    public async Task AdminAccess_CanBeGrantedListedAndRevoked()
    {
        var bob = await Data.MemberAsync(100, "Bob", 'B');
        var admin = await Data.RoleAsync("Admin");

        Assert.True(await Repository.CreateAdminAccessAsync(bob, admin));
        var setting = Assert.Single(await Repository.GetAdminAccessSettingsAsync());
        Assert.Equal((bob, "Bob", "Bobland", 'B', admin, "Admin"),
            (setting.AccountId, setting.RulerName, setting.NationName, setting.VepRole, setting.AdminRoleId, setting.AdminRoleName));

        Assert.True(await Repository.DeleteAdminAccessAsync(bob, admin));
        Assert.Empty(await Repository.GetAdminAccessSettingsAsync());
        Assert.False(await Repository.DeleteAdminAccessAsync(bob, admin));
    }

    [Fact]
    public async Task CreateAdminAccess_RejectsDuplicates()
    {
        var bob = await Data.MemberAsync(100, "Bob", 'B');
        var admin = await Data.RoleAsync("Admin");
        await Repository.CreateAdminAccessAsync(bob, admin);

        await Assert.ThrowsAsync<MySqlException>(() => Repository.CreateAdminAccessAsync(bob, admin));
    }
}
