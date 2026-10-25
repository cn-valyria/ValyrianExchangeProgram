using MySql.Data.MySqlClient;
using Valyria.Core.DTOs;
using Valyria.Infrastructure.Repositories;

namespace Valyria.IntegrationTests;

public class AdminRepositoryTests(MySqlFixture fixture) : RepositoryTestBase(fixture)
{
    private AdminRepository Repository => new(Fixture.VepConnectionString);

    /// <summary>
    /// A buyer and seller with an active deal, a donor, a flagged seller, and an on-hold seller who is ignored
    /// everywhere. One of the buyer's slots is edited to receive 2 tech cycles
    /// </summary>
    private async Task<(int Buyer, int Seller, int Donor, int Flagged, int OnHold)> SeedProgramAsync()
    {
        var buyer = await Data.MemberAsync(100, "Bob", 'B');
        var seller = await Data.MemberAsync(200, "Sue", 'S', fm: true);
        var donor = await Data.MemberAsync(300, "Dan", 'D');
        var flagged = await Data.MemberAsync(400, "Fay", 'S');
        var onHold = await Data.MemberAsync(500, "Hal", 'H');
        var buyerSlots = await Data.SlotIdsAsync(buyer);
        var sellerSlots = await Data.SlotIdsAsync(seller);
        await Data.DealAsync(buyerSlots[0], sellerSlots[0]);
        await Data.DealAsync(buyerSlots[1], sellerSlots[1], status: "cancelled");
        var flaggedDeal = await Data.DealAsync(buyerSlots[2], (await Data.SlotIdsAsync(flagged))[0], status: "cancelled");
        await Data.DealFlagAsync(flaggedDeal, "5_day_replacement", flagged, buyer);
        await Data.DealFlagAsync(flaggedDeal, "1_day_inactive", buyer, flagged, resolvedOn: DateTime.UtcNow);
        await Data.ExecuteAsync("update vep_db.slot set tech_received_cycles = 2 where id = @id", new { id = buyerSlots[3] });
        return (buyer, seller, donor, flagged, onHold);
    }

    [Fact]
    public async Task ProgramSummary_CountsActiveAccountsDealsAndFlags()
    {
        await SeedProgramAsync();

        var summary = await Repository.GetProgramSummaryAsync();

        Assert.Equal((4, 1, 1, 1), (summary.TotalAccounts, summary.ActiveDeals, summary.OpenFlags, summary.FlaggedAccounts));
    }

    [Fact]
    public async Task SlotConfigs_GroupSlotsByCycleValues_SplittingOpenSlotsOnFlaggedAccounts()
    {
        await SeedProgramAsync();

        var configs = await Repository.GetSlotConfigsAsync();

        // (tech sent, tech received, cash sent, cash received): accounts, total, in deals, open, open accounts, flagged open
        Assert.Equal(
            [
                ((1, 0, 0, 1), (2, 9, 1, 4, 1, 4)),
                ((0, 0, 1, 0), (1, 4, 0, 4, 1, 0)),
                ((0, 1, 1, 0), (1, 3, 1, 2, 1, 0)),
                ((0, 2, 1, 0), (1, 1, 0, 1, 1, 0)),
            ],
            configs.Select(c => (
                (c.TechSentCycles, c.TechReceivedCycles, c.CashSentCycles, c.CashReceivedCycles),
                (c.Accounts, c.TotalSlots, c.SlotsInDeals, c.OpenSlots, c.OpenAccounts, c.FlaggedOpenSlots))));
    }

    [Fact]
    public async Task SlotConfigs_IsEmpty_WithNoAccounts()
    {
        Assert.Empty(await Repository.GetSlotConfigsAsync());
    }

    [Fact]
    public async Task DealHealth_BucketsActiveDealsBySlotPairing()
    {
        var today = await Data.CnTodayAsync();
        var bob = await Data.MemberAsync(100, "Bob", 'B');
        var sue = await Data.MemberAsync(200, "Sue", 'S', fm: true);
        var ann = await Data.MemberAsync(300, "Ann", 'B');
        var sam = await Data.MemberAsync(400, "Sam", 'S');
        var bobSlots = await Data.SlotIdsAsync(bob);
        var sueSlots = await Data.SlotIdsAsync(sue);
        var annSlots = await Data.SlotIdsAsync(ann);

        // No deal_state row yet
        await Data.DealAsync(bobSlots[0], sueSlots[0]);
        var inFlight = await Data.DealAsync(bobSlots[1], sueSlots[1]);
        await Data.DealStateAsync(inFlight, "in_flight", sue, bob);
        var sendToday = await Data.DealAsync(bobSlots[2], sueSlots[2]);
        await Data.DealStateAsync(sendToday, "awaiting_send", bob, sue, responsible: bob, responsibleSince: today);
        var stalled = await Data.DealAsync(bobSlots[3], sueSlots[3]);
        await Data.DealStateAsync(stalled, "pending_start", bob, sue, stateSince: today.AddDays(-6));
        // Ann's slots come after Sue's, so Sue's seller slot is slot 1 here
        var blocked = await Data.DealAsync(annSlots[0], sueSlots[4]);
        await Data.DealStateAsync(blocked, "blocked", ann, sue, responsible: sue, responsibleSince: today, blockedReason: "receiver_slots_full");
        var late = await Data.DealAsync(annSlots[1], (await Data.SlotIdsAsync(sam))[0]);
        await Data.DealStateAsync(late, "awaiting_send", ann, sam, responsible: ann, responsibleSince: today.AddDays(-2));
        await Data.DealAsync(annSlots[2], (await Data.SlotIdsAsync(sam))[1], status: "cancelled");

        var health = await Repository.GetDealHealthAsync();

        Assert.Equal(
            [
                ("B", "S", DealHealth.OnTrack, 2),
                ("B", "S", DealHealth.Starting, 1),
                ("B", "S", DealHealth.Stalled, 1),
                ("B", "S", DealHealth.AtRisk, 1),
                ("S", "B", DealHealth.AtRisk, 1),
            ],
            health
                .Select(h => (h.Slot1CashSentCycles == 1 ? "B" : "S", h.Slot2CashSentCycles == 1 ? "B" : "S", h.Health, h.Deals))
                .Order());
    }

    /// <summary>
    /// Buyer Bob and seller Sue in Valyria, seller Rex in Rogues, buyer Nia in no alliance, and an on-hold
    /// account in Rogues. Bob has a deal with Rex (no deal_state yet) and an in-flight deal with Sue
    /// </summary>
    private async Task SeedAlliancesAsync()
    {
        await Data.AllianceAsync(1, "Valyria");
        await Data.AllianceAsync(2, "Rogues");
        var bob = await Data.MemberAsync(100, "Bob", 'B', allianceId: 1);
        var sue = await Data.MemberAsync(200, "Sue", 'S', allianceId: 1);
        var rex = await Data.MemberAsync(300, "Rex", 'S', allianceId: 2);
        await Data.MemberAsync(400, "Nia", 'B');
        await Data.MemberAsync(500, "Hal", 'H', allianceId: 2);
        var bobSlots = await Data.SlotIdsAsync(bob);
        await Data.DealAsync(bobSlots[0], (await Data.SlotIdsAsync(rex))[0]);
        var withinValyria = await Data.DealAsync(bobSlots[1], (await Data.SlotIdsAsync(sue))[0]);
        await Data.DealStateAsync(withinValyria, "in_flight", sue, bob);
    }

    [Fact]
    public async Task AllianceSlotConfigs_GroupSlotConfigsByAlliance()
    {
        await SeedAlliancesAsync();
        var sue = await Data.ScalarAsync<int>("select id from vep_db.account where nation_id = 200");
        await Data.ExecuteAsync("update vep_db.slot set tech_sent_cycles = 2 where account_id = @sue and slot_number = 4", new { sue });

        var configs = await Repository.GetAllianceSlotConfigsAsync();

        // (alliance, alliance accounts, tech sent, cash sent): total, in deals, open. Sue's slots are in two configs
        Assert.Equal(
            [
                ("No alliance", 1, 0, 1, (4, 0, 4)),
                ("Rogues", 1, 1, 0, (4, 1, 3)),
                ("Valyria", 2, 0, 1, (4, 2, 2)),
                ("Valyria", 2, 1, 0, (3, 1, 2)),
                ("Valyria", 2, 2, 0, (1, 0, 1)),
            ],
            configs.Select(c => (c.AllianceName, c.AllianceAccounts, c.TechSentCycles, c.CashSentCycles,
                (c.TotalSlots, c.SlotsInDeals, c.OpenSlots))));
    }

    [Fact]
    public async Task AllianceDealHealth_CountsEachDealOncePerAllianceInvolved()
    {
        await SeedAlliancesAsync();

        var health = await Repository.GetAllianceDealHealthAsync();

        Assert.Equal(
            [
                ("Rogues", false, DealHealth.Starting, 1),
                ("Valyria", false, DealHealth.Starting, 1),
                ("Valyria", true, DealHealth.OnTrack, 1),
            ],
            health.Select(h => (h.AllianceName, h.WithinAlliance, h.Health, h.Deals)).Order());
    }

    [Fact]
    public async Task DealActivity_CountsEachCnDay_IncludingQuietDays()
    {
        var today = await Data.CnTodayAsync();
        var bob = await Data.MemberAsync(100, "Bob", 'B');
        var sue = await Data.MemberAsync(200, "Sue", 'S');
        var bobSlots = await Data.SlotIdsAsync(bob);
        var sueSlots = await Data.SlotIdsAsync(sue);
        // Deal times are server UTC; noon CN time is 17:00 UTC
        var yesterdayDeal = await Data.DealAsync(bobSlots[0], sueSlots[0], createdOn: today.AddDays(-1).AddHours(17));
        var todayDeal = await Data.DealAsync(bobSlots[1], sueSlots[1], createdOn: today.AddHours(17));
        await Data.ExecuteAsync("update vep_db.deal set status = 'cancelled', cancelled_on = @at where id = @id",
            new { id = todayDeal, at = today.AddHours(17) });
        await Data.DealActivityAsync(yesterdayDeal, 1, 100, 200, today.AddDays(-1));
        var failedLeg = await Data.DealActivityAsync(todayDeal, 1, 100, 200, today, failureReason: "expired");
        await Data.ExecuteAsync("update vep_db.deal_activity set failed_on = @today where id = @id", new { id = failedLeg, today });
        await Data.DealActivityAsync(yesterdayDeal, 2, 200, 100, today.AddDays(-5));

        var activity = await Repository.GetDealActivityAsync(3);

        Assert.Equal([today.AddDays(-2), today.AddDays(-1), today], activity.Select(a => a.Day));
        Assert.Equal([(0, 0, 0, 0), (1, 0, 1, 0), (1, 1, 1, 1)],
            activity.Select(a => (a.LegsSent, a.LegsFailed, a.DealsCreated, a.DealsCancelled)));
    }

    [Fact]
    public async Task CancellationReasons_CountRecentCancellations_GroupingAdminReasons()
    {
        var today = await Data.CnTodayAsync();
        var bob = await Data.MemberAsync(100, "Bob", 'B');
        var sue = await Data.MemberAsync(200, "Sue", 'S', fm: true);
        var bobSlots = await Data.SlotIdsAsync(bob);
        var sueSlots = await Data.SlotIdsAsync(sue);
        async Task CancelAsync(int slot, int partnerSlot, string by, string reason, int daysAgo)
        {
            var deal = await Data.DealAsync(slot, partnerSlot);
            await Data.ExecuteAsync(@"
                update vep_db.deal
                set status = 'cancelled', cancelled_by = @by, cancellation_reason = @reason, cancelled_on = @at
                where id = @deal",
                new { deal, by, reason, at = today.AddDays(-daysAgo).AddHours(17) });
        }
        await CancelAsync(bobSlots[0], sueSlots[0], "system", "Nation deleted", 0);
        await CancelAsync(bobSlots[1], sueSlots[1], "system", "Nation deleted", 29);
        await CancelAsync(bobSlots[2], sueSlots[2], "zach", "Swapping partners", 3);
        await CancelAsync(bobSlots[3], sueSlots[3], "system", "Account role changed", 30);
        await Data.DealAsync(bobSlots[0], sueSlots[4]);

        var reasons = await Repository.GetCancellationReasonsAsync(30);

        Assert.Equal([("Nation deleted", 2), ("Cancelled by an admin", 1)], reasons.Select(r => (r.Reason, r.Deals)));
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
