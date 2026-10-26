using Valyria.Core.DTOs;
using Valyria.Core.DTOs.CN;
using Valyria.Infrastructure.Repositories;

namespace Valyria.IntegrationTests;

/// <summary>
/// Team +1 aid slot events (V17): bonus slots, slot totals, matching cutoffs, and cancelling bonus deals
/// </summary>
public class TeamEventTests(MySqlFixture fixture) : RepositoryTestBase(fixture)
{
    private const int Aqua = (int)Team.Aqua;
    private const int Red = (int)Team.Red;

    private AdminRepository AdminRepository => new(Fixture.VepConnectionString);
    private DealsRepository DealsRepository => new(Fixture.VepConnectionString);
    private DataLoaderRepository LoaderRepository => new(Fixture.VepConnectionString);

    private record DealRow(int Id, string Status, string? CancellationReason);

    private Task<int> SlotsTotalAsync(int accountId) => Data.ScalarAsync<int>(
        "select slots_total from vep_db.account_deal_summary where account_id = @accountId", new { accountId });

    private Task<int?> BonusSlotIdAsync(int accountId) => Data.ScalarAsync<int?>(
        "select id from vep_db.slot where account_id = @accountId and is_team_bonus", new { accountId });

    private async Task<DealRow> DealAsync(int dealId) => (await Data.QueryAsync<DealRow>(
        "select id as Id, status as Status, cancellation_reason as CancellationReason from vep_db.deal where id = @dealId",
        new { dealId })).Single();

    private async Task<int[]> ActiveDealSlotsAsync(int accountId) => (await Data.QueryAsync<int>(@"
        select s.slot_number
        from vep_db.deal d
        join vep_db.slot s on s.id in (d.slot_1_id, d.slot_2_id) and s.account_id = @accountId
        where d.status = 'active'
        order by s.slot_number", new { accountId })).ToArray();

    /// <summary>
    /// Sets every regular slot of the account to all-zero cycles, so only its bonus slot can be matched
    /// </summary>
    private Task OnlyBonusSlotMatchableAsync(int accountId) => Data.ExecuteAsync(@"
        update vep_db.slot
        set tech_sent_cycles = 0, tech_received_cycles = 0, cash_sent_cycles = 0, cash_received_cycles = 0
        where account_id = @accountId and not is_team_bonus", new { accountId });

    private Task SetCyclesAsync(int slotId, int techSent, int techReceived, int cashSent, int cashReceived) => Data.ExecuteAsync(@"
        update vep_db.slot
        set tech_sent_cycles = @techSent, tech_received_cycles = @techReceived,
            cash_sent_cycles = @cashSent, cash_received_cycles = @cashReceived
        where id = @slotId", new { slotId, techSent, techReceived, cashSent, cashReceived });

    // --- Regression: no events -------------------------------------------------------------------------------

    [Fact]
    public async Task WithNoEvents_SlotTotalsAreStillFourPlusFmPlusDra_AndNoBonusSlotsAppear()
    {
        var plain = await Data.MemberAsync(100, "Plain");
        var fm = await Data.MemberAsync(200, "Fm", fm: true);
        var both = await Data.MemberAsync(300, "Both", fm: true, dra: true);

        Assert.Equal(0, await Data.SyncTeamEventSlotsAsync());

        Assert.Equal(new[] { 4, 5, 6 }, new[] { await SlotsTotalAsync(plain), await SlotsTotalAsync(fm), await SlotsTotalAsync(both) });
        Assert.Equal(15, await Data.ScalarAsync<int>("select count(1) from vep_db.slot"));
        Assert.Equal(0, await Data.ScalarAsync<int>("select count(1) from vep_db.slot where is_team_bonus"));

        // update_deal_state checks full slots against the same totals
        var deal = await Data.DealAsync((await Data.SlotIdsAsync(plain))[0], (await Data.SlotIdsAsync(both))[0]);
        Assert.Equal((4, 6), (await Data.QueryAsync<(int, int)>(
            "select slots_total_1, slots_total_2 from vep_db.deal_sequence where deal_id = @deal", new { deal })).Single());
    }

    [Fact]
    public async Task WithNoEvents_MatchingFillsOnlyRegularSlots()
    {
        var buyer = await Data.MemberAsync(100, "Buyer", 'B');
        for (var i = 0; i < 6; i++)
            await Data.MemberAsync(200 + i, $"Seller{i}", 'S', team: Red);

        var result = await LoaderRepository.MatchDealsAsync();

        Assert.Equal(4, result.DealsCreated);
        Assert.Equal(new[] { 1, 2, 3, 4 }, await ActiveDealSlotsAsync(buyer));
    }

    // --- Who gets a bonus slot ---------------------------------------------------------------------------------

    [Fact]
    public async Task ActiveEvent_GivesOnlyTheTeamsAccountsABonusSlot()
    {
        var cnToday = await Data.CnTodayAsync();
        var aquaFm = await Data.MemberAsync(100, "AquaFm", 'S', fm: true);
        var red = await Data.MemberAsync(200, "Red", 'B', team: Red);
        var aquaOnHold = await Data.MemberAsync(300, "AquaHold", 'B');
        await Data.ExecuteAsync("update vep_db.account set va = 'H' where id = @aquaOnHold", new { aquaOnHold });
        await Data.TeamEventAsync(Aqua, cnToday.AddDays(-5));

        await Data.SyncTeamEventSlotsAsync();

        Assert.Equal(6, await SlotsTotalAsync(aquaFm));
        Assert.Equal(4, await SlotsTotalAsync(red));
        Assert.Equal(4, await SlotsTotalAsync(aquaOnHold));
        Assert.Null(await BonusSlotIdAsync(red));
        Assert.Null(await BonusSlotIdAsync(aquaOnHold));

        // Slot 7, with the seller's role defaults
        var slots = (await DealsRepository.GetAccountSlotsAsync(aquaFm)).Slots;
        var bonus = Assert.Single(slots, slot => slot.IsTeamBonus);
        Assert.Equal(7, bonus.SlotNumber);
        Assert.Equal((1, 0, 0, 1), (bonus.TechSentCycles, bonus.TechReceivedCycles, bonus.CashSentCycles, bonus.CashReceivedCycles));
        Assert.Equal(cnToday.AddDays(25), bonus.TeamBonusEndsOn);
        Assert.Equal([1, 2, 3, 4, 5, 7], slots.Select(slot => (int)slot.SlotNumber));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(-30, true)] // Day 30 is the last day: a proposal passed on 10/6 lasts through 11/5
    [InlineData(-31, false)]
    public async Task EventIsInEffect_FromItsApprovalDateThroughThirtyDaysLater(int approvedDaysAgo, bool inEffect)
    {
        var cnToday = await Data.CnTodayAsync();
        var account = await Data.MemberAsync(100, "Aqua");
        await Data.TeamEventAsync(Aqua, cnToday.AddDays(approvedDaysAgo));

        await Data.SyncTeamEventSlotsAsync();

        Assert.Equal(inEffect ? 5 : 4, await SlotsTotalAsync(account));
        Assert.Equal(inEffect, await BonusSlotIdAsync(account) != null);
    }

    [Fact]
    public async Task DeletedEvents_GiveNoBonus()
    {
        var cnToday = await Data.CnTodayAsync();
        var account = await Data.MemberAsync(100, "Aqua");
        var teamEvent = await Data.TeamEventAsync(Aqua, cnToday);
        await Data.ExecuteAsync("update vep_db.team_event set deleted_on = now(), deleted_by = 'admin' where id = @teamEvent", new { teamEvent });

        await Data.SyncTeamEventSlotsAsync();

        Assert.Equal(4, await SlotsTotalAsync(account));
    }

    [Fact]
    public async Task FmAndDraChanges_LeaveTheBonusSlotAndItsDealAlone()
    {
        var cnToday = await Data.CnTodayAsync();
        var aqua = await Data.MemberAsync(100, "Aqua", 'B', fm: true);
        var partner = await Data.MemberAsync(200, "Partner", 'S', team: Red);
        await Data.TeamEventAsync(Aqua, cnToday);
        await Data.SyncTeamEventSlotsAsync();
        var bonusDeal = await Data.DealAsync((await BonusSlotIdAsync(aqua))!.Value, (await Data.SlotIdsAsync(partner))[0]);

        await Data.ExecuteAsync("update vep_db.account set fm = 0, dra = 1 where id = @aqua", new { aqua });
        await Data.ExecuteAsync("update vep_db.account set dra = 0 where id = @aqua", new { aqua });

        Assert.Equal("active", (await DealAsync(bonusDeal)).Status);
        Assert.Equal(5, await SlotsTotalAsync(aqua));
        Assert.Equal(new[] { 1, 2, 3, 4, 7 }, (await DealsRepository.GetAccountSlotsAsync(aqua)).Slots.Select(slot => (int)slot.SlotNumber));
    }

    // --- Matching ----------------------------------------------------------------------------------------------

    [Fact]
    public async Task Matching_UsesTheBonusSlot_OnlyOnceTheRegularSlotsAreFull()
    {
        var cnToday = await Data.CnTodayAsync();
        var buyer = await Data.MemberAsync(100, "Buyer", 'B');
        for (var i = 0; i < 6; i++)
            await Data.MemberAsync(200 + i, $"Seller{i}", 'S', team: Red);
        await Data.TeamEventAsync(Aqua, cnToday.AddDays(-3));
        await Data.SyncTeamEventSlotsAsync();

        var result = await LoaderRepository.MatchDealsAsync();

        Assert.Equal(5, result.DealsCreated);
        Assert.Equal(new[] { 1, 2, 3, 4, 7 }, await ActiveDealSlotsAsync(buyer));
    }

    [Theory]
    // One-way (donor): matched for as long as the event lasts
    [InlineData(0, 0, 1, 0, -30, true)]
    // 1 cash : 1 tech: through day 20
    [InlineData(0, 1, 1, 0, -20, true)]
    [InlineData(0, 1, 1, 0, -21, false)]
    // 1 cash : 2 tech: through day 10
    [InlineData(0, 2, 1, 0, -10, true)]
    [InlineData(0, 2, 1, 0, -11, false)]
    public async Task Matching_StopsFillingABonusSlot_OnceItsLegsCantAllBeSentBeforeTheEventEnds(
        int techSent, int techReceived, int cashSent, int cashReceived, int approvedDaysAgo, bool matched)
    {
        var cnToday = await Data.CnTodayAsync();
        var aqua = await Data.MemberAsync(100, "Aqua", 'B');
        var partner = await Data.MemberAsync(200, "Partner", 'S', team: Red);
        await Data.TeamEventAsync(Aqua, cnToday.AddDays(approvedDaysAgo));
        await Data.SyncTeamEventSlotsAsync();
        await OnlyBonusSlotMatchableAsync(aqua);
        await SetCyclesAsync((await BonusSlotIdAsync(aqua))!.Value, techSent, techReceived, cashSent, cashReceived);
        await SetCyclesAsync((await Data.SlotIdsAsync(partner))[0], techReceived, techSent, cashReceived, cashSent);

        var result = await LoaderRepository.MatchDealsAsync();

        Assert.Equal(matched ? 1 : 0, result.DealsCreated);
        var teamEvent = (await DealsRepository.GetAccountDealsAsync(aqua)).TeamEvent;
        Assert.NotNull(teamEvent);
        Assert.Equal(cnToday.AddDays(approvedDaysAgo + 30 - 10 * (techSent + techReceived + cashSent + cashReceived - 1)),
            teamEvent.BonusSlotMatchableUntil);
    }

    [Fact]
    public async Task Matching_AppliesTheCutoff_WhenThePartnerOwnsTheBonusSlot()
    {
        var cnToday = await Data.CnTodayAsync();
        await Data.MemberAsync(100, "Red", 'B', team: Red);
        var aqua = await Data.MemberAsync(200, "Aqua", 'S');
        await Data.TeamEventAsync(Aqua, cnToday.AddDays(-21));
        await Data.SyncTeamEventSlotsAsync();
        await OnlyBonusSlotMatchableAsync(aqua);

        Assert.Equal(0, (await LoaderRepository.MatchDealsAsync()).DealsCreated);
    }

    // --- Ending bonus slots ------------------------------------------------------------------------------------

    /// <summary>
    /// Aqua account with a regular deal and a deal on its bonus slot
    /// </summary>
    private async Task<(int Aqua, int TeamEvent, int RegularDeal, int BonusDeal, int BonusSlot)> SeedBonusDealAsync()
    {
        var cnToday = await Data.CnTodayAsync();
        var aqua = await Data.MemberAsync(100, "Aqua", 'B');
        var partner1 = await Data.MemberAsync(200, "Partner1", 'S', team: Red);
        var partner2 = await Data.MemberAsync(300, "Partner2", 'S', team: Red);
        var teamEvent = await Data.TeamEventAsync(Aqua, cnToday.AddDays(-5));
        await Data.SyncTeamEventSlotsAsync();
        var bonusSlot = (await BonusSlotIdAsync(aqua))!.Value;
        var regularDeal = await Data.DealAsync((await Data.SlotIdsAsync(aqua))[0], (await Data.SlotIdsAsync(partner1))[0]);
        var bonusDeal = await Data.DealAsync(bonusSlot, (await Data.SlotIdsAsync(partner2))[0]);
        return (aqua, teamEvent, regularDeal, bonusDeal, bonusSlot);
    }

    [Fact]
    public async Task EventEnding_CancelsOnlyTheBonusDeal_AndKeepsTheSlotForItsHistory()
    {
        var (aqua, teamEvent, regularDeal, bonusDeal, bonusSlot) = await SeedBonusDealAsync();
        var cnToday = await Data.CnTodayAsync();
        await Data.ExecuteAsync("update vep_db.team_event set approved_on = @approvedOn where id = @teamEvent",
            new { teamEvent, approvedOn = cnToday.AddDays(-31) });

        Assert.Equal(1, await Data.SyncTeamEventSlotsAsync());

        Assert.Equal(new DealRow(bonusDeal, "cancelled", "Team event ended"), await DealAsync(bonusDeal));
        Assert.Equal("active", (await DealAsync(regularDeal)).Status);
        Assert.Equal(4, await SlotsTotalAsync(aqua));
        Assert.Equal(bonusSlot, await BonusSlotIdAsync(aqua));
        Assert.DoesNotContain((await DealsRepository.GetAccountSlotsAsync(aqua)).Slots, slot => slot.IsTeamBonus);
        Assert.Null((await DealsRepository.GetAccountDealsAsync(aqua)).TeamEvent);
    }

    [Fact]
    public async Task LeavingTheTeam_CancelsTheBonusDeal_AndRejoiningReusesTheSlot()
    {
        var (aqua, _, regularDeal, bonusDeal, bonusSlot) = await SeedBonusDealAsync();
        await Data.ExecuteAsync("update cybernations_db.nation set team = @Red where id = 100", new { Red });

        Assert.Equal(1, await Data.SyncTeamEventSlotsAsync());

        Assert.Equal(new DealRow(bonusDeal, "cancelled", "Nation left the Aqua team"), await DealAsync(bonusDeal));
        Assert.Equal("active", (await DealAsync(regularDeal)).Status);
        Assert.Equal(4, await SlotsTotalAsync(aqua));

        // Nation audit rows are keyed on (id, second), so a second change this second would collide
        await Data.ExecuteAsync("delete from cybernations_db.audit_nation where nation_id = 100");
        await Data.ExecuteAsync("update cybernations_db.nation set team = @Aqua where id = 100", new { Aqua });
        await Data.SyncTeamEventSlotsAsync();

        Assert.Equal(5, await SlotsTotalAsync(aqua));
        Assert.Equal(bonusSlot, await BonusSlotIdAsync(aqua));
    }

    [Fact]
    public async Task UnusedBonusSlots_AreRemoved_WhenTheBonusEnds()
    {
        var cnToday = await Data.CnTodayAsync();
        var aqua = await Data.MemberAsync(100, "Aqua");
        var teamEvent = await Data.TeamEventAsync(Aqua, cnToday);
        await Data.SyncTeamEventSlotsAsync();
        Assert.NotNull(await BonusSlotIdAsync(aqua));

        await Data.ExecuteAsync("update vep_db.team_event set approved_on = @approvedOn where id = @teamEvent",
            new { teamEvent, approvedOn = cnToday.AddDays(-40) });
        Assert.Equal(0, await Data.SyncTeamEventSlotsAsync());

        Assert.Null(await BonusSlotIdAsync(aqua));
    }

    [Fact]
    public async Task Sync_LeavesBonusDealsAlone_WhenTheNationTableIsEmpty()
    {
        var (_, _, _, bonusDeal, _) = await SeedBonusDealAsync();
        await Data.ExecuteAsync("delete from cybernations_db.nation");

        Assert.Equal(0, await Data.SyncTeamEventSlotsAsync());

        Assert.Equal("active", (await DealAsync(bonusDeal)).Status);
    }

    [Fact]
    public async Task LoaderRun_CancelsEndedBonusDeals()
    {
        var (_, teamEvent, regularDeal, bonusDeal, _) = await SeedBonusDealAsync();
        var cnToday = await Data.CnTodayAsync();
        await Data.ExecuteAsync("update vep_db.team_event set approved_on = @approvedOn where id = @teamEvent",
            new { teamEvent, approvedOn = cnToday.AddDays(-31) });

        var result = await LoaderRepository.UpdateDealStateAsync(checkInactivity: false);

        Assert.Equal(1, result.TeamBonusDealsCancelled);
        Assert.Equal("cancelled", (await DealAsync(bonusDeal)).Status);
        Assert.Equal("active", (await DealAsync(regularDeal)).Status);
    }

    [Fact]
    public async Task DealsCancelledWhenTheBonusEnded_CantBeRestored()
    {
        var (aqua, teamEvent, _, bonusDeal, _) = await SeedBonusDealAsync();
        var partner2 = await Data.ScalarAsync<int>("select id from vep_db.account where nation_id = 300");
        await Data.ExecuteAsync("update vep_db.deal set status = 'cancelled', cancelled_on = now(), cancelled_by = 'system' where id = @bonusDeal",
            new { bonusDeal });
        var flag = await Data.DealFlagAsync(bonusDeal, "5_day_replacement", aqua, partner2, "awaiting_send");

        Assert.True((await DealsRepository.GetFlaggedDealsAsync()).Single(f => f.Id == flag).CanRestoreDeal);

        await Data.ExecuteAsync("update vep_db.team_event set approved_on = @approvedOn where id = @teamEvent",
            new { teamEvent, approvedOn = (await Data.CnTodayAsync()).AddDays(-31) });
        await Data.SyncTeamEventSlotsAsync();

        var detail = (await DealsRepository.GetFlaggedDealsAsync()).Single(f => f.Id == flag);
        Assert.True(detail.FlaggedSlot.IsInactiveTeamBonus);
        Assert.False(detail.CanRestoreDeal);
    }

    // --- Deal state --------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(false, "blocked")]
    [InlineData(true, "awaiting_send")]
    public async Task UpdateDealState_CountsTheBonusSlot_WhenCheckingForFullSlots(bool eventActive, string expectedState)
    {
        var cnToday = await Data.CnTodayAsync();
        var bob = await Data.MemberAsync(100, "Bob", 'B');
        var sue = await Data.MemberAsync(200, "Sue", 'S', team: Red);
        var deal = await Data.DealAsync((await Data.SlotIdsAsync(bob))[0], (await Data.SlotIdsAsync(sue))[0],
            createdOn: DateTime.UtcNow.AddDays(-3));
        await Data.DealStateAsync(deal, "awaiting_send", nextSender: bob, nextReceiver: sue, responsible: bob,
            responsibleSince: cnToday.AddDays(-2), stateSince: cnToday.AddDays(-2));
        await Data.ExecuteAsync("update vep_db.deal_state set activated_on = @activatedOn where deal_id = @deal",
            new { deal, activatedOn = cnToday.AddDays(-2) });
        if (eventActive)
            await Data.TeamEventAsync(Aqua, cnToday.AddDays(-10));

        // Bob fills his 4 regular slots with aid outside VEP, after the deal
        for (var i = 0; i < 4; i++)
            await Data.AidAsync(900 + i, 100, 500 + i, cnToday.AddDays(-1), reason: "Financial Assistance");

        await Data.ExecuteAsync("call vep_db.update_deal_state()");

        Assert.Equal(expectedState, await Data.ScalarAsync<string>(
            "select state from vep_db.deal_state where deal_id = @deal", new { deal }));
    }

    // --- Account and deal pages --------------------------------------------------------------------------------

    [Fact]
    public async Task DealPages_ShowWhenBonusDealsEnd_OnBothSides()
    {
        var (aqua, _, regularDeal, bonusDeal, _) = await SeedBonusDealAsync();
        var cnToday = await Data.CnTodayAsync();
        var partner2 = await Data.ScalarAsync<int>("select id from vep_db.account where nation_id = 300");

        var mine = await DealsRepository.GetAccountDealsAsync(aqua);
        Assert.Equal(5, mine.SlotsTotal);
        Assert.NotNull(mine.TeamEvent);
        // A buyer's bonus slot is 1:1, so it's matched through day 20
        Assert.Equal((Team.Aqua, cnToday.AddDays(-5), cnToday.AddDays(25), cnToday.AddDays(15)),
            (mine.TeamEvent.Team, mine.TeamEvent.ApprovedOn, mine.TeamEvent.EndsOn, mine.TeamEvent.BonusSlotMatchableUntil));
        Assert.Null(mine.ActiveDeals.Single(d => d.Id == regularDeal).TeamBonusEndsOn);
        Assert.Equal(cnToday.AddDays(25), mine.ActiveDeals.Single(d => d.Id == bonusDeal).TeamBonusEndsOn);

        // The partner isn't on the team, but its deal still ends with the event
        var theirs = await DealsRepository.GetAccountDealsAsync(partner2);
        Assert.Null(theirs.TeamEvent);
        Assert.Equal(cnToday.AddDays(25), Assert.Single(theirs.ActiveDeals).TeamBonusEndsOn);

        var adminDeals = (await DealsRepository.GetActiveDealsAsync(null, 50, 0)).Results;
        Assert.Equal(cnToday.AddDays(25), adminDeals.Single(d => d.Id == bonusDeal).TeamBonusEndsOn);
        Assert.Null(adminDeals.Single(d => d.Id == regularDeal).TeamBonusEndsOn);

        var dealById = await DealsRepository.GetDealByIdAsync(bonusDeal);
        var bonusSlot = Assert.Single(new[] { dealById.Slot1, dealById.Slot2 }, slot => slot.IsTeamBonus);
        Assert.Equal(cnToday.AddDays(25), bonusSlot.TeamBonusEndsOn);
    }

    [Fact]
    public async Task AdminDashboardSlotConfigs_CountBonusSlotsOnlyWhileInEffect()
    {
        var (_, teamEvent, _, _, _) = await SeedBonusDealAsync();
        // 3 accounts x 4 regular slots, plus the bonus slot
        Assert.Equal(13, (await AdminRepository.GetSlotConfigsAsync()).Sum(c => c.TotalSlots));

        await Data.ExecuteAsync("update vep_db.team_event set approved_on = @approvedOn where id = @teamEvent",
            new { teamEvent, approvedOn = (await Data.CnTodayAsync()).AddDays(-31) });
        await Data.SyncTeamEventSlotsAsync();

        Assert.Equal(12, (await AdminRepository.GetSlotConfigsAsync()).Sum(c => c.TotalSlots));
    }

    // --- Admin settings ----------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateTeamEvent_GivesTheTeamBonusSlotsStraightAway_AndListsTheEvent()
    {
        var cnToday = await Data.CnTodayAsync();
        var aqua1 = await Data.MemberAsync(100, "Aqua1");
        await Data.MemberAsync(200, "Aqua2", 'S');
        var red = await Data.MemberAsync(300, "Red", team: Red);

        var result = await AdminRepository.CreateTeamEventAsync(
            new TeamEventCreateRequest { Team = Team.Aqua, ApprovedOn = cnToday.AddDays(-2) }, "admin");

        Assert.Equal(TeamEventCreateResult.Created, result);
        Assert.Equal(5, await SlotsTotalAsync(aqua1));
        Assert.Equal(4, await SlotsTotalAsync(red));

        var listed = Assert.Single(await AdminRepository.GetTeamEventsAsync());
        Assert.Equal(Team.Aqua, listed.Team);
        Assert.Equal(cnToday.AddDays(-2), listed.ApprovedOn);
        Assert.Equal(cnToday.AddDays(28), listed.EndsOn);
        Assert.True(listed.IsActive);
        Assert.Equal(2, listed.AccountsWithBonusSlot);
        Assert.Equal(0, listed.ActiveBonusDeals);
        Assert.Equal("admin", listed.CreatedBy);
    }

    [Fact]
    public async Task CreateTeamEvent_RejectsGreyFutureAndOverlappingEvents()
    {
        var cnToday = await Data.CnTodayAsync();
        await Data.TeamEventAsync(Aqua, cnToday.AddDays(-40));

        Assert.Equal(TeamEventCreateResult.InvalidTeam, await AdminRepository.CreateTeamEventAsync(
            new TeamEventCreateRequest { Team = Team.None, ApprovedOn = cnToday }, "admin"));
        Assert.Equal(TeamEventCreateResult.ApprovedInFuture, await AdminRepository.CreateTeamEventAsync(
            new TeamEventCreateRequest { Team = Team.Red, ApprovedOn = cnToday.AddDays(1) }, "admin"));
        // The earlier event lasts through 10 days ago
        Assert.Equal(TeamEventCreateResult.Overlaps, await AdminRepository.CreateTeamEventAsync(
            new TeamEventCreateRequest { Team = Team.Aqua, ApprovedOn = cnToday.AddDays(-10) }, "admin"));
        Assert.Equal(TeamEventCreateResult.Created, await AdminRepository.CreateTeamEventAsync(
            new TeamEventCreateRequest { Team = Team.Aqua, ApprovedOn = cnToday.AddDays(-9) }, "admin"));
        // Another team's events don't count
        Assert.Equal(TeamEventCreateResult.Created, await AdminRepository.CreateTeamEventAsync(
            new TeamEventCreateRequest { Team = Team.Red, ApprovedOn = cnToday.AddDays(-9) }, "admin"));

        var listed = await AdminRepository.GetTeamEventsAsync();
        Assert.Equal(3, listed.Count);
        Assert.False(listed.Single(e => e.ApprovedOn == cnToday.AddDays(-40)).IsActive);
    }

    [Fact]
    public async Task DeleteTeamEvent_CancelsItsBonusDealsStraightAway()
    {
        var (aqua, teamEvent, regularDeal, bonusDeal, _) = await SeedBonusDealAsync();
        Assert.Equal(1, Assert.Single(await AdminRepository.GetTeamEventsAsync()).ActiveBonusDeals);

        Assert.True(await AdminRepository.DeleteTeamEventAsync(teamEvent, "admin"));

        Assert.Equal(new DealRow(bonusDeal, "cancelled", "Team event removed by an admin"), await DealAsync(bonusDeal));
        Assert.Equal("active", (await DealAsync(regularDeal)).Status);
        Assert.Equal(4, await SlotsTotalAsync(aqua));
        Assert.Empty(await AdminRepository.GetTeamEventsAsync());
        Assert.False(await AdminRepository.DeleteTeamEventAsync(teamEvent, "admin"));
    }
}
