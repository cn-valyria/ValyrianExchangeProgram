using Valyria.Core.DTOs;
using Valyria.Core.Entities;
using Valyria.Core.Interfaces;
using Valyria.Infrastructure.Repositories;

namespace Valyria.IntegrationTests;

public class DealsRepositoryTests(MySqlFixture fixture) : RepositoryTestBase(fixture)
{
    private const int BobNation = 100;
    private const int SueNation = 200;

    private DealsRepository Repository => new(Fixture.VepConnectionString);

    /// <summary>
    /// Bob (buyer, Valyria) and Sue (seller, Rogues) with a deal between their first slots
    /// </summary>
    private async Task<(int Bob, int Sue, int Deal)> SeedPairAsync(string status = "active")
    {
        await Data.AllianceAsync(1, "Valyria");
        await Data.AllianceAsync(2, "Rogues");
        var bob = await Data.MemberAsync(BobNation, "Bob", 'B', allianceId: 1);
        var sue = await Data.MemberAsync(SueNation, "Sue", 'S', allianceId: 2, discord: "sue#2");
        var deal = await Data.DealAsync((await Data.SlotIdsAsync(bob))[0], (await Data.SlotIdsAsync(sue))[0], status);
        return (bob, sue, deal);
    }

    [Fact]
    public async Task GetAccountDeals_DefaultsToFourOpenSlots_ForAnUnknownAccount()
    {
        var summary = await Repository.GetAccountDealsAsync(12345);

        Assert.Equal((0, 4, 4, false), (summary.SlotsUsed, summary.SlotsTotal, summary.SlotsAvailable, summary.SlotsFull));
        Assert.Empty(summary.ActiveDeals);
        Assert.Empty(summary.Notifications);
    }

    [Fact]
    public async Task GetAccountDeals_ShowsTheDealFromEachSidesPerspective()
    {
        var (bob, sue, deal) = await SeedPairAsync();
        var cnToday = await Data.CnTodayAsync();
        await Data.DealStateAsync(deal, "awaiting_send", nextSender: bob, nextReceiver: sue,
            responsible: bob, responsibleSince: cnToday.AddDays(-2), stateSince: cnToday.AddDays(-2));
        await Data.DealActivityAsync(deal, 1, SueNation, BobNation, cnToday.AddDays(-12), approvedOn: cnToday.AddDays(-11));
        await Data.DealFlagAsync(deal, "1_day_inactive", bob, sue, "awaiting_send");

        var bobView = await Repository.GetAccountDealsAsync(bob);
        var sueView = await Repository.GetAccountDealsAsync(sue);

        Assert.Equal((1, 4, 3, false), (bobView.SlotsUsed, bobView.SlotsTotal, bobView.SlotsAvailable, bobView.SlotsFull));
        var bobDeal = Assert.Single(bobView.ActiveDeals);
        Assert.Equal(deal, bobDeal.Id);
        Assert.Equal(DealActivityState.AwaitingSend, bobDeal.State);
        Assert.Equal(cnToday.AddDays(-2), bobDeal.StateSince);
        Assert.Equal(cnToday.AddDays(-12), bobDeal.LastActivityDate);
        Assert.True(bobDeal.IsMyTurnToSend);
        Assert.True(bobDeal.IAmResponsible);
        Assert.False(bobDeal.PartnerIsResponsible);
        Assert.Equal(2, bobDeal.DaysResponsible);
        Assert.Equal(cnToday.AddDays(3), bobDeal.CancelsOn);
        Assert.NotNull(bobDeal.FlaggedOn);
        Assert.Equal("sue#2", bobDeal.PartnerDiscord);
        Assert.Equal((bob, "Bob", "Valyria", (byte)1), (bobDeal.MySlot.Account.AccountId, bobDeal.MySlot.Account.RulerName, bobDeal.MySlot.Account.AllianceName, bobDeal.MySlot.SlotNumber));
        Assert.Equal((sue, "Sueland", "Rogues"), (bobDeal.PartnerSlot.Account.AccountId, bobDeal.PartnerSlot.Account.NationName, bobDeal.PartnerSlot.Account.AllianceName));
        Assert.Equal(1, bobDeal.MySlot.TechReceivedCycles);
        Assert.Equal(1, bobDeal.PartnerSlot.TechSentCycles);

        var sueDeal = Assert.Single(sueView.ActiveDeals);
        Assert.False(sueDeal.IsMyTurnToSend);
        Assert.False(sueDeal.IAmResponsible);
        Assert.True(sueDeal.PartnerIsResponsible);
        Assert.Null(sueDeal.FlaggedOn);
        Assert.Equal(sue, sueDeal.MySlot.Account.AccountId);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task GetAccountDeals_DescribesTheLegInFlight(bool approved, bool linkedLate)
    {
        var (bob, sue, deal) = await SeedPairAsync();
        var cnToday = await Data.CnTodayAsync();
        await Data.AidAsync(900, SueNation, BobNation, cnToday, status: approved ? 2 : 1, technology: 50);
        await Data.DealActivityAsync(deal, 1, SueNation, BobNation, cnToday, aidId: 900,
            approvedOn: approved ? cnToday : null, linkedLate: linkedLate);
        await Data.DealStateAsync(deal, "in_flight", nextSender: bob, nextReceiver: sue,
            inFlightAidId: 900, inFlightUntil: cnToday.AddDays(10));

        var bobDeal = Assert.Single((await Repository.GetAccountDealsAsync(bob)).ActiveDeals);
        var sueDeal = Assert.Single((await Repository.GetAccountDealsAsync(sue)).ActiveDeals);

        Assert.Equal(DealActivityState.InFlight, bobDeal.State);
        Assert.Equal(cnToday.AddDays(10), bobDeal.InFlightUntil);
        Assert.True(bobDeal.InFlightIsIncoming);
        Assert.False(sueDeal.InFlightIsIncoming);
        Assert.Equal(approved || linkedLate, bobDeal.InFlightAccepted);
        Assert.Null(bobDeal.DaysResponsible);
    }

    [Fact]
    public async Task GetAccountDeals_DescribesABlockedDeal()
    {
        var (bob, sue, deal) = await SeedPairAsync();
        var cnToday = await Data.CnTodayAsync();
        await Data.AidAsync(901, SueNation, BobNation, cnToday.AddDays(-1));
        await Data.DealStateAsync(deal, "blocked", nextSender: bob, nextReceiver: sue, responsible: sue,
            responsibleSince: cnToday, blockedReason: "out_of_sequence_aid", blockingAidId: 901);

        var bobDeal = Assert.Single((await Repository.GetAccountDealsAsync(bob)).ActiveDeals);

        Assert.Equal(DealActivityState.Blocked, bobDeal.State);
        Assert.Equal(DealBlockedReason.OutOfSequenceAid, bobDeal.BlockedReason);
        Assert.Equal(cnToday.AddDays(9), bobDeal.BlockedUntil);
        Assert.True(bobDeal.PartnerIsResponsible);
        Assert.Equal(0, bobDeal.DaysResponsible);
    }

    [Fact]
    public async Task GetAccountDeals_LeavesStateEmpty_UntilTheDataLoaderPicksUpANewDeal()
    {
        var (bob, _, _) = await SeedPairAsync();

        var bobDeal = Assert.Single((await Repository.GetAccountDealsAsync(bob)).ActiveDeals);

        Assert.Null(bobDeal.State);
        Assert.Null(bobDeal.LastActivityDate);
        Assert.False(bobDeal.IsMyTurnToSend);
    }

    [Fact]
    public async Task GetAccountDeals_LeavesCancelledDealsOut()
    {
        var (bob, _, _) = await SeedPairAsync(status: "cancelled");

        var summary = await Repository.GetAccountDealsAsync(bob);

        Assert.Empty(summary.ActiveDeals);
        Assert.Equal(0, summary.SlotsUsed);
    }

    [Theory]
    [InlineData("aid_failed", "aid_expired", true, "error", "an aid offer between you expired without being accepted")]
    [InlineData("aid_failed", "aid_cancelled", true, "error", "an aid offer between you was cancelled")]
    [InlineData("5_day_replacement", "sender_slots_full", true, "error", "because your aid slots were full of other aid for 5 days. You won't be matched")]
    [InlineData("5_day_replacement", "out_of_sequence_aid", true, "error", "because aid you sent out of turn blocked it")]
    [InlineData("5_day_replacement", "awaiting_send", true, "error", "because you didn't send the expected aid")]
    [InlineData("5_day_replacement", "receiver_slots_full", false, "info", "because their aid slots were full of other aid for 5 days. Your slot will be matched")]
    [InlineData("5_day_replacement", "out_of_sequence_aid", false, "info", "because aid they sent out of turn blocked it")]
    [InlineData("5_day_replacement", null, false, "info", "because they didn't send the expected aid")]
    public async Task GetAccountDeals_ExplainsCancelledDeals(string flagType, string? reason, bool flaggedMe, string type, string expectedText)
    {
        var (bob, sue, deal) = await SeedPairAsync(status: "cancelled");
        await Data.DealFlagAsync(deal, flagType, flaggedMe ? bob : sue, flaggedMe ? sue : bob, reason);

        var notification = Assert.Single((await Repository.GetAccountDealsAsync(bob)).Notifications);

        Assert.Equal(type, notification.Type);
        Assert.Equal(deal, notification.DealId);
        Assert.StartsWith("Your deal with Sueland was cancelled", notification.Message);
        Assert.Contains(expectedText, notification.Message);
    }

    [Fact]
    public async Task GetAccountDeals_FallsBackToYourPartner_WhenThePartnersNationIsGone()
    {
        var (bob, sue, deal) = await SeedPairAsync(status: "cancelled");
        await Data.ExecuteAsync("delete from cybernations_db.nation where id = @SueNation", new { SueNation });
        await Data.DealFlagAsync(deal, "5_day_replacement", bob, sue, "awaiting_send");

        var notification = Assert.Single((await Repository.GetAccountDealsAsync(bob)).Notifications);

        Assert.StartsWith("Your deal with your partner was cancelled", notification.Message);
    }

    [Fact]
    public async Task GetAccountDeals_OnlyNotifiesAboutRecentUnresolvedCancellations()
    {
        var (bob, sue, deal) = await SeedPairAsync(status: "cancelled");
        // Innocent partners stop hearing about a replacement after 10 days, warnings show on the deal itself,
        // and resolved flags are done with
        await Data.DealFlagAsync(deal, "5_day_replacement", sue, bob, "awaiting_send", flaggedOn: DateTime.UtcNow.AddDays(-11));
        await Data.DealFlagAsync(deal, "1_day_inactive", bob, sue, "awaiting_send");
        await Data.DealFlagAsync(deal, "aid_failed", bob, sue, "aid_expired", resolvedOn: DateTime.UtcNow);

        Assert.Empty((await Repository.GetAccountDealsAsync(bob)).Notifications);
    }

    [Fact]
    public async Task GetAccountDealHistory_PagesThroughEveryDeal_NewestFirst()
    {
        var (bob, sue, firstDeal) = await SeedPairAsync(status: "cancelled");
        var bobSlots = await Data.SlotIdsAsync(bob);
        var sueSlots = await Data.SlotIdsAsync(sue);
        var secondDeal = await Data.DealAsync(bobSlots[1], sueSlots[1], createdOn: DateTime.UtcNow.AddDays(1));
        var thirdDeal = await Data.DealAsync(bobSlots[2], sueSlots[2], createdOn: DateTime.UtcNow.AddDays(2));
        var cnToday = await Data.CnTodayAsync();
        await Data.DealActivityAsync(secondDeal, 1, BobNation, SueNation, cnToday.AddDays(-3));
        await Data.DealActivityAsync(secondDeal, 2, SueNation, BobNation, cnToday.AddDays(-2));
        await Data.DealActivityAsync(secondDeal, 3, BobNation, SueNation, cnToday);

        var firstPage = await Repository.GetAccountDealHistoryAsync(bob, limit: 2, offset: 0);
        var secondPage = await Repository.GetAccountDealHistoryAsync(bob, limit: 2, offset: 2);

        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal([thirdDeal, secondDeal], firstPage.Results.Select(d => d.Id));
        Assert.Equal([firstDeal], secondPage.Results.Select(d => d.Id));

        var withActivity = firstPage.Results[1];
        Assert.Equal((2, 1, cnToday, (byte)2), (withActivity.AidSentCount, withActivity.AidReceivedCount, withActivity.LastActivityDate!.Value, withActivity.MySlotNumber));
        Assert.Equal(DealStatus.Active, withActivity.Status);
        Assert.Equal((sue, "Sue", "Rogues"), (withActivity.Partner.AccountId, withActivity.Partner.RulerName, withActivity.Partner.AllianceName));
        Assert.Equal(DealStatus.Cancelled, secondPage.Results[0].Status);
        Assert.NotNull(secondPage.Results[0].CancelledOn);
    }

    [Fact]
    public async Task GetDealActivity_ReturnsNull_WhenTheAccountIsNotPartOfTheDeal()
    {
        var (_, _, deal) = await SeedPairAsync();
        var outsider = await Data.MemberAsync(300, "Eve");

        Assert.Null(await Repository.GetDealActivityAsync(deal, outsider));
    }

    [Fact]
    public async Task GetDealActivity_ReportsEachLegsDirectionAndOutcome()
    {
        var (bob, _, deal) = await SeedPairAsync();
        var day = await Data.CnTodayAsync();
        await Data.AidAsync(901, BobNation, SueNation, day.AddDays(-4), money: 3_000_000);
        await Data.AidAsync(902, SueNation, BobNation, day.AddDays(-3), technology: 50);
        var accepted = await Data.DealActivityAsync(deal, 1, BobNation, SueNation, day.AddDays(-4), aidId: 901, approvedOn: day.AddDays(-4));
        var late = await Data.DealActivityAsync(deal, 2, SueNation, BobNation, day.AddDays(-3), aidId: 902, linkedLate: true);
        var cancelled = await Data.DealActivityAsync(deal, 3, BobNation, SueNation, day.AddDays(-2), failureReason: "cancelled");
        var expired = await Data.DealActivityAsync(deal, 4, SueNation, BobNation, day.AddDays(-1), failureReason: "expired");
        var pending = await Data.DealActivityAsync(deal, 5, BobNation, SueNation, day);

        var activity = await Repository.GetDealActivityAsync(deal, bob);

        Assert.Equal([pending, expired, cancelled, late, accepted], activity.Select(a => a.Id));
        Assert.Equal(
            [DealLegOutcome.Pending, DealLegOutcome.Expired, DealLegOutcome.Cancelled, DealLegOutcome.Accepted, DealLegOutcome.Accepted],
            activity.Select(a => a.Outcome));
        Assert.Equal([true, false, true, false, true], activity.Select(a => a.IsOutgoing));
        Assert.Equal((3_000_000, 0, 901), (activity[4].Money, activity[4].Technology, activity[4].AidId!.Value));
        Assert.Equal((0, 50), (activity[3].Money, activity[3].Technology));
        Assert.Equal((0, 0, (int?)null), (activity[0].Money, activity[0].Technology, activity[0].AidId));
    }

    [Fact]
    public async Task GetDealById_ReturnsBothSides()
    {
        var (bob, sue, deal) = await SeedPairAsync();

        var result = await Repository.GetDealByIdAsync(deal);

        Assert.Equal(DealStatus.Active, result.Status);
        Assert.Equal("system", result.CreatedBy);
        Assert.Equal((bob, "Valyria", true), (result.Slot1.Account.AccountId, result.Slot1.Account.AllianceName, result.Slot1.HasActiveDeal));
        Assert.Equal((sue, "Rogues", true), (result.Slot2.Account.AccountId, result.Slot2.Account.AllianceName, result.Slot2.HasActiveDeal));
        Assert.Equal(result.Slot1Id, result.Slot1.Id);
    }

    [Fact]
    public async Task GetDealById_ReturnsNull_WhenMissing()
    {
        Assert.Null(await Repository.GetDealByIdAsync(12345));
    }

    [Fact]
    public async Task CreateDeal_OrdersTheSlots_AndRecordsWhoCreatedIt()
    {
        await Data.MemberAsync(BobNation, "Bob", 'B');
        await Data.MemberAsync(SueNation, "Sue", 'S');
        var bobSlot = (await Data.SlotIdsAsync(await Data.ScalarAsync<int>("select id from account where nation_id = 100")))[0];
        var sueSlot = (await Data.SlotIdsAsync(await Data.ScalarAsync<int>("select id from account where nation_id = 200")))[0];

        var dealId = await Repository.CreateDealAsync(new CreateDealRequest { Slot1Id = sueSlot, Slot2Id = bobSlot, CreatedBy = "admin" });

        var deal = await Repository.GetDealByIdAsync(dealId);
        Assert.Equal((bobSlot, sueSlot, "admin"), (deal.Slot1Id, deal.Slot2Id, deal.CreatedBy));
    }

    [Fact]
    public async Task CancelDeal_RecordsWhoCancelledItAndWhy()
    {
        var (_, _, deal) = await SeedPairAsync();

        await Repository.CancelDealAsync(deal, "partner left", "admin");

        var result = await Repository.GetDealByIdAsync(deal);
        Assert.Equal((DealStatus.Cancelled, "admin", "partner left"), (result.Status, result.CancelledBy, result.CancellationReason));
        Assert.NotNull(result.CancelledOn);
        Assert.False(result.Slot1.HasActiveDeal);
    }

    [Fact]
    public async Task GetWaitQueue_IsOrderedByPriority()
    {
        await Data.AllianceAsync(1, "Valyria");
        var low = await Data.MemberAsync(100, "Low", allianceId: 1);
        var high = await Data.MemberAsync(200, "High");
        await Data.WaitQueueAsync(low, 1, 1.5m);
        await Data.WaitQueueAsync(high, 3, 9.25m, nextSlotOpensOn: new DateTime(2026, 10, 20));

        var queue = await Repository.GetWaitQueueAsync();

        Assert.Equal([high, low], queue.Select(q => q.AccountId));
        Assert.Equal((3, 9.25m, new DateTime(2026, 10, 20)), (queue[0].SlotsNeeded, queue[0].PriorityScore, queue[0].NextSlotOpensOn!.Value));
        Assert.Equal(("High", (string?)null), (queue[0].Account.RulerName, queue[0].Account.AllianceName));
        Assert.Equal("Valyria", queue[1].Account.AllianceName);
    }

    [Fact]
    public async Task GetActiveDeals_ListsActiveDeals_LongestHeldUpFirst()
    {
        var (bob, sue, firstDeal) = await SeedPairAsync();
        var bobSlots = await Data.SlotIdsAsync(bob);
        var sueSlots = await Data.SlotIdsAsync(sue);
        var heldUpDeal = await Data.DealAsync(bobSlots[1], sueSlots[1]);
        await Data.DealAsync(bobSlots[2], sueSlots[2], status: "cancelled");
        var cnToday = await Data.CnTodayAsync();
        await Data.DealStateAsync(heldUpDeal, "awaiting_send", nextSender: sue, nextReceiver: bob, responsible: sue, responsibleSince: cnToday.AddDays(-1));
        await Data.DealFlagAsync(heldUpDeal, "1_day_inactive", sue, bob, "awaiting_send");

        var page = await Repository.GetActiveDealsAsync(search: "  ", limit: 25, offset: 0);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal([heldUpDeal, firstDeal], page.Results.Select(d => d.Id));
        var heldUp = page.Results[0];
        Assert.Equal((DealActivityState.AwaitingSend, sue, sue, 1, true), (heldUp.State!.Value, heldUp.NextSenderAccountId!.Value, heldUp.ResponsibleAccountId!.Value, heldUp.DaysResponsible!.Value, heldUp.HasOpenFlag));
        Assert.Equal(("Bob", "Sue"), (heldUp.Slot1.Account.RulerName, heldUp.Slot2.Account.RulerName));
        Assert.Null(page.Results[1].State);
        Assert.False(page.Results[1].HasOpenFlag);
    }

    [Theory]
    [InlineData("Bobl", 1)]
    [InlineData("sue", 1)]
    [InlineData("Rogue", 1)]
    [InlineData("Valyria", 1)]
    [InlineData("nobody", 0)]
    public async Task GetActiveDeals_SearchesNationRulerAndAllianceOnEitherSide(string search, int expectedCount)
    {
        await SeedPairAsync();

        var page = await Repository.GetActiveDealsAsync(search, 25, 0);

        Assert.Equal(expectedCount, page.TotalCount);
        Assert.Equal(expectedCount, page.Results.Count);
    }

    [Fact]
    public async Task GetActiveDeals_Pages()
    {
        var (bob, sue, _) = await SeedPairAsync();
        var bobSlots = await Data.SlotIdsAsync(bob);
        var sueSlots = await Data.SlotIdsAsync(sue);
        await Data.DealAsync(bobSlots[1], sueSlots[1]);
        await Data.DealAsync(bobSlots[2], sueSlots[2]);

        var page = await Repository.GetActiveDealsAsync(null, limit: 2, offset: 2);

        Assert.Equal(3, page.TotalCount);
        Assert.Single(page.Results);
    }

    /// <summary>
    /// Bob and Sue are VEP accounts, Hal is on hold, Eve and Zed aren't in the VEP.
    /// Aid 1 (Bob to Sue) is matched to a deal; the rest aren't.
    /// </summary>
    private async Task<int> SeedAidActivityAsync()
    {
        var (bob, sue, deal) = await SeedPairAsync();
        await Data.MemberAsync(400, "Hal", 'H');
        await Data.NationAsync(300, "Eveland", "Eve", allianceId: 2);
        await Data.NationAsync(500, "Zedland", "Zed");
        await Data.AidAsync(1, BobNation, SueNation, new DateTime(2026, 10, 1, 12, 0, 0), money: 3_000_000);
        await Data.AidAsync(2, SueNation, 300, new DateTime(2026, 10, 2, 12, 0, 0), technology: 50);
        await Data.AidAsync(3, 300, BobNation, new DateTime(2026, 10, 3, 23, 59, 0), soldiers: 100);
        await Data.AidAsync(4, 400, 300, new DateTime(2026, 10, 4, 12, 0, 0));
        await Data.AidAsync(5, 300, 500, new DateTime(2026, 10, 5, 12, 0, 0));
        await Data.DealActivityAsync(deal, 1, BobNation, SueNation, new DateTime(2026, 10, 1), aidId: 1);
        return deal;
    }

    private Task<AidActivityPage> SearchAid(string? search = null, string? direction = null, DateTime? since = null,
        DateTime? until = null, bool includeOnHold = false, bool unmatchedOnly = false, int limit = 25, int offset = 0) =>
        Repository.SearchAidActivityAsync(new AidActivityFilters
        {
            Search = search, Direction = direction, SentSince = since, SentUntil = until,
            IncludeOnHold = includeOnHold, UnmatchedOnly = unmatchedOnly
        }, limit, offset);

    [Fact]
    public async Task SearchAidActivity_ListsAidInvolvingVepAccounts_NewestFirst()
    {
        var deal = await SeedAidActivityAsync();

        var page = await SearchAid();

        Assert.Equal(3, page.TotalCount);
        Assert.Equal([3, 2, 1], page.Results.Select(a => a.AidId));
        var matched = page.Results[2];
        Assert.Equal((deal, 3_000_000, 2, "VEP"), (matched.DealId!.Value, matched.Money, matched.Status, matched.Reason));
        Assert.Equal((BobNation, "Bob", "Valyria", "B"), (matched.Sender.NationId, matched.Sender.RulerName, matched.Sender.AllianceName, matched.Sender.Role));
        Assert.NotNull(matched.Sender.AccountId);
        var fromOutsider = page.Results[0];
        Assert.Null(fromOutsider.DealId);
        Assert.Equal(("Eve", (int?)null, (string?)null), (fromOutsider.Sender.RulerName, fromOutsider.Sender.AccountId, fromOutsider.Sender.Role));
        Assert.Equal(100, fromOutsider.Soldiers);
    }

    [Fact]
    public async Task SearchAidActivity_FiltersOnHoldUnmatchedAndDates()
    {
        await SeedAidActivityAsync();

        Assert.Equal([4, 3, 2, 1], (await SearchAid(includeOnHold: true)).Results.Select(a => a.AidId));
        Assert.Equal([3, 2], (await SearchAid(unmatchedOnly: true)).Results.Select(a => a.AidId));
        // SentUntil includes the whole day
        Assert.Equal([3, 2], (await SearchAid(since: new DateTime(2026, 10, 2), until: new DateTime(2026, 10, 3))).Results.Select(a => a.AidId));
    }

    [Theory]
    [InlineData("Sue", null, new[] { 2, 1 })]
    [InlineData("Sue", "sent", new[] { 2 })]
    [InlineData("Sue", "RECEIVED", new[] { 1 })]
    [InlineData("Rogues", "received", new[] { 2, 1 })]
    [InlineData(" Bobland ", "sent", new[] { 1 })]
    public async Task SearchAidActivity_SearchesTheRequestedSide(string search, string? direction, int[] expected)
    {
        await SeedAidActivityAsync();

        Assert.Equal(expected, (await SearchAid(search, direction)).Results.Select(a => a.AidId));
    }

    [Fact]
    public async Task SearchAidActivity_Pages()
    {
        await SeedAidActivityAsync();

        var page = await SearchAid(limit: 1, offset: 1);

        Assert.Equal(3, page.TotalCount);
        Assert.Equal([2], page.Results.Select(a => a.AidId));
    }

    [Fact]
    public async Task FlaggedDeals_ListsOpenFlags_UntilResolved()
    {
        var (bob, sue, deal) = await SeedPairAsync();
        var older = await Data.DealFlagAsync(deal, "1_day_inactive", bob, sue, "receiver_slots_full", flaggedOn: DateTime.UtcNow.AddDays(-1));
        var newer = await Data.DealFlagAsync(deal, "aid_failed", sue, bob, "aid_expired");
        await Data.DealFlagAsync(deal, "5_day_replacement", bob, sue, null, resolvedOn: DateTime.UtcNow);

        var flags = await Repository.GetFlaggedDealsAsync();

        Assert.Equal([newer, older], flags.Select(f => f.Id));
        Assert.Equal((DealFlagType.AidFailed, DealFlagReason.AidExpired), (flags[0].FlagType, flags[0].FlagReason!.Value));
        Assert.Equal((DealFlagType.OneDayInactive, DealFlagReason.ReceiverSlotsFull), (flags[1].FlagType, flags[1].FlagReason!.Value));
        Assert.Equal((bob, "Valyria", sue, "Rogues"), (flags[1].FlaggedAccount.AccountId, flags[1].FlaggedAccount.AllianceName, flags[1].InnocentAccount.AccountId, flags[1].InnocentAccount.AllianceName));
        Assert.Equal(deal, flags[1].DealId);

        await Repository.ResolveDealFlagAsync(newer, "admin", "partner was on vacation");

        Assert.Equal([older], (await Repository.GetFlaggedDealsAsync()).Select(f => f.Id));
        Assert.Equal(("admin", "partner was on vacation"),
            (await Data.QueryAsync<(string, string)>("select resolved_by, admin_notes from deal_flag where id = @newer", new { newer })).Single());
    }

    [Fact]
    public async Task FlaggedDeals_MapsEveryFlagType()
    {
        var (bob, sue, deal) = await SeedPairAsync();
        await Data.DealFlagAsync(deal, "5_day_replacement", bob, sue, "sender_slots_full");
        await Data.DealFlagAsync(deal, "aid_failed", bob, sue, "aid_cancelled");
        await Data.DealFlagAsync(deal, "1_day_inactive", bob, sue, "awaiting_send");
        await Data.DealFlagAsync(deal, "1_day_inactive", bob, sue, "out_of_sequence_aid");
        await Data.DealFlagAsync(deal, "1_day_inactive", bob, sue, null);

        var flags = await Repository.GetFlaggedDealsAsync();

        Assert.Equal(
            [DealFlagType.OneDayInactive, DealFlagType.FiveDayReplacement, DealFlagType.AidFailed],
            flags.Select(f => f.FlagType).Distinct().OrderBy(t => t).ToArray());
        Assert.Equal(
            [null, DealFlagReason.AwaitingSend, DealFlagReason.SenderSlotsFull, DealFlagReason.OutOfSequenceAid, DealFlagReason.AidCancelled],
            flags.Select(f => f.FlagReason).OrderBy(r => r));
    }

    [Fact]
    public async Task FlaggedDeals_DescribeTheDeal_AndWhoIsHoldingItUp()
    {
        var (bob, sue, deal) = await SeedPairAsync();
        var bobSlots = await Data.SlotIdsAsync(bob);
        var sueSlots = await Data.SlotIdsAsync(sue);
        var cnToday = await Data.CnTodayAsync();
        await Data.DealActivityAsync(deal, 1, BobNation, SueNation, cnToday.AddDays(-13), approvedOn: cnToday.AddDays(-13));
        await Data.DealActivityAsync(deal, 2, SueNation, BobNation, cnToday.AddDays(-12), approvedOn: cnToday.AddDays(-12));
        await Data.DealActivityAsync(deal, 3, BobNation, SueNation, cnToday.AddDays(-3), failureReason: "cancelled", failedOn: cnToday.AddDays(-2));
        await Data.DealStateAsync(deal, "awaiting_send", nextSender: bob, nextReceiver: sue, responsible: bob,
            responsibleSince: cnToday.AddDays(-2));
        var warning = await Data.DealFlagAsync(deal, "1_day_inactive", bob, sue, "awaiting_send");
        // An older deal between them, cancelled over Bob holding it up
        var oldDeal = await Data.DealAsync(bobSlots[1], sueSlots[1], "cancelled");
        await Data.ExecuteAsync("update vep_db.deal set cancelled_by = 'system' where id = @oldDeal", new { oldDeal });
        var replacement = await Data.DealFlagAsync(oldDeal, "5_day_replacement", bob, sue, "awaiting_send");

        var flags = await Repository.GetFlaggedDealsAsync();

        var active = flags.Single(f => f.Id == warning);
        Assert.Equal((DealStatus.Active, DealActivityState.AwaitingSend, bob, bob, 2, cnToday.AddDays(3)),
            (active.DealStatus, active.State!.Value, active.NextSenderAccountId!.Value, active.ResponsibleAccountId!.Value,
                active.DaysResponsible!.Value, active.CancelsOn!.Value));
        Assert.Equal((bobSlots[0], bob, sueSlots[0], sue),
            (active.FlaggedSlot.Id, active.FlaggedSlot.Account.AccountId, active.InnocentSlot.Id, active.InnocentSlot.Account.AccountId));
        Assert.Equal(("B", "S", 2), (active.FlaggedAccountRole, active.InnocentAccountRole, active.FlaggedAccountOpenFlagCount));
        Assert.Equal((2, cnToday.AddDays(-13)), (active.LegsCompleted, active.FlaggedLastSentOn!.Value));
        Assert.False(active.CanRestoreDeal);

        // The old deal's slots are free, but Bob and Sue already have another deal together
        var cancelled = flags.Single(f => f.Id == replacement);
        Assert.Equal((DealStatus.Cancelled, "system"), (cancelled.DealStatus, cancelled.DealCancelledBy));
        Assert.Null(cancelled.State);
        Assert.Equal((null, null, deal), (cancelled.FlaggedSlotOtherDealId, cancelled.InnocentSlotOtherDealId, cancelled.PairOtherDealId));
        Assert.True(cancelled.SlotsStillMatch);
        Assert.False(cancelled.CanRestoreDeal);
    }

    [Fact]
    public async Task FlaggedDeals_StillShowAccountsWhoseNationWasDeleted()
    {
        var (bob, sue, deal) = await SeedPairAsync();
        await Data.DealFlagAsync(deal, "1_day_inactive", bob, sue, "awaiting_send");
        await Data.ExecuteAsync("delete from cybernations_db.nation where id = @BobNation", new { BobNation });

        var flag = (await Repository.GetFlaggedDealsAsync()).Single();

        Assert.Equal((bob, BobNation, null), (flag.FlaggedAccount.AccountId, flag.FlaggedAccount.NationId, flag.FlaggedAccount.RulerName));
        Assert.Equal("Sue", flag.InnocentAccount.RulerName);
    }

    /// <summary>
    /// Bob and Sue's deal, cancelled by VEP with a 5-day flag against Bob
    /// </summary>
    private async Task<(int Bob, int Sue, int Deal, int Flag)> SeedCancelledByFlagAsync()
    {
        var (bob, sue, deal) = await SeedPairAsync("cancelled");
        await Data.ExecuteAsync(@"
            update vep_db.deal
            set cancelled_by = 'system', cancellation_reason = 'Expected aid not sent for 5+ days - automatically cancelled'
            where id = @deal", new { deal });
        var flag = await Data.DealFlagAsync(deal, "5_day_replacement", bob, sue, "awaiting_send");
        return (bob, sue, deal, flag);
    }

    [Theory]
    [InlineData("nothing")]
    [InlineData("flagged slot rematched")]
    [InlineData("partner slot rematched")]
    [InlineData("pair has another deal")]
    [InlineData("partner on hold")]
    public async Task FlaggedDeals_SayWhetherTheCancelledDealCanBeRestored(string blocker)
    {
        var (bob, sue, _, flagId) = await SeedCancelledByFlagAsync();
        var bobSlots = await Data.SlotIdsAsync(bob);
        var sueSlots = await Data.SlotIdsAsync(sue);
        var carol = await Data.MemberAsync(300, "Carol", 'S');
        int? otherDeal = blocker switch
        {
            "flagged slot rematched" => await Data.DealAsync(bobSlots[0], (await Data.SlotIdsAsync(carol))[0]),
            "partner slot rematched" => await Data.DealAsync(sueSlots[0], (await Data.SlotIdsAsync(carol))[0]),
            "pair has another deal" => await Data.DealAsync(bobSlots[1], sueSlots[1]),
            _ => null
        };
        if (blocker == "partner on hold")
            await Data.ExecuteAsync("update vep_db.account set va = 'H' where id = @sue", new { sue });

        var flag = (await Repository.GetFlaggedDealsAsync()).Single(f => f.Id == flagId);

        Assert.Equal(blocker == "flagged slot rematched" ? otherDeal : null, flag.FlaggedSlotOtherDealId);
        Assert.Equal(blocker == "partner slot rematched" ? otherDeal : null, flag.InnocentSlotOtherDealId);
        Assert.Equal(blocker == "pair has another deal" ? otherDeal : null, flag.PairOtherDealId);
        Assert.Equal(blocker == "partner on hold", flag.InnocentAccountOnHold);
        // Going on hold also resets the account's slots, so they no longer match
        Assert.Equal(blocker != "partner on hold", flag.SlotsStillMatch);
        Assert.Equal(blocker == "nothing", flag.CanRestoreDeal);
    }

    [Fact]
    public async Task FlagEvidence_ShowsTheLegs_AndWhatHeldTheFlaggedNationsSlots()
    {
        var (bob, sue, deal) = await SeedPairAsync();
        var cnToday = await Data.CnTodayAsync();
        // Deal times are server UTC; CN noon is 17:00 UTC
        await Data.ExecuteAsync("update vep_db.deal set created_on = @createdOn where id = @deal",
            new { deal, createdOn = cnToday.AddDays(-5).AddHours(17) });
        var carol = await Data.MemberAsync(300, "Carol", 'S');
        var carolDeal = await Data.DealAsync((await Data.SlotIdsAsync(bob))[1], (await Data.SlotIdsAsync(carol))[0]);

        DateTime CnNoon(int daysAgo) => cnToday.AddDays(-daysAgo).AddHours(12);
        await Data.AidAsync(801, BobNation, SueNation, CnNoon(4), money: 9_000_000);
        await Data.AidAsync(802, SueNation, BobNation, CnNoon(3), technology: 100);
        await Data.AidAsync(803, BobNation, 300, CnNoon(2), money: 9_000_000);
        await Data.AidAsync(804, BobNation, 500, CnNoon(6), money: 1_000_000, reason: "Before the deal");
        await Data.AidAsync(805, BobNation, 500, CnNoon(1), money: 2_000_000, reason: "Outside VEP");
        await Data.AidAsync(806, BobNation, 500, CnNoon(11), money: 3_000_000);
        await Data.AidAsync(807, BobNation, 500, CnNoon(1), status: 3);
        await Data.DealActivityAsync(deal, 1, BobNation, SueNation, cnToday.AddDays(-4), aidId: 801, approvedOn: cnToday.AddDays(-4));
        await Data.DealActivityAsync(deal, 2, SueNation, BobNation, cnToday.AddDays(-3), aidId: 802);
        await Data.DealActivityAsync(carolDeal, 1, BobNation, 300, cnToday.AddDays(-2), aidId: 803);
        var flagId = await Data.DealFlagAsync(deal, "1_day_inactive", bob, sue, "sender_slots_full");

        var evidence = await Repository.GetDealFlagEvidenceAsync(flagId);

        Assert.Equal([(1, true, 9_000_000, 0, DealLegOutcome.Accepted), (2, false, 0, 100, DealLegOutcome.Pending)],
            evidence.Legs.Select(l => (l.LegNumber, l.SentByFlaggedAccount, l.Money, l.Technology, l.Outcome)));
        Assert.Equal((cnToday, 4), (evidence.SlotsAsOf, evidence.FlaggedSlotsTotal));
        Assert.Equal([804, 801, 802, 803, 805], evidence.FlaggedSlotAid.Select(a => a.AidId));
        Assert.Equal([null, deal, deal, carolDeal, null], evidence.FlaggedSlotAid.Select(a => a.DealId));
        Assert.Equal([true, false, false, false, false], evidence.FlaggedSlotAid.Select(a => a.SentBeforeDeal));
        Assert.Equal([true, true, false, true, true], evidence.FlaggedSlotAid.Select(a => a.IsOutgoing));
        Assert.Equal((SueNation, "Sue"), (evidence.FlaggedSlotAid[2].OtherNation.NationId, evidence.FlaggedSlotAid[2].OtherNation.RulerName));
        Assert.Equal((500, null), (evidence.FlaggedSlotAid[0].OtherNation.NationId, evidence.FlaggedSlotAid[0].OtherNation.RulerName));

        // Once the deal has ended, the slots are shown as they were when the flag was raised: CN evening two days ago
        await Data.ExecuteAsync("update vep_db.deal set status = 'cancelled' where id = @deal", new { deal });
        await Data.ExecuteAsync("update vep_db.deal_flag set flagged_on = @flaggedOn where id = @flagId",
            new { flagId, flaggedOn = cnToday.AddDays(-2).AddHours(23) });
        await Data.ExecuteAsync("update cybernations_db.aid set status = 4 where id = 804");

        var asFlagged = await Repository.GetDealFlagEvidenceAsync(flagId);

        // 806 was still inside its 10 days then, and 805 hadn't been sent yet
        Assert.Equal(cnToday.AddDays(-2), asFlagged.SlotsAsOf);
        Assert.Equal([806, 804, 801, 802, 803], asFlagged.FlaggedSlotAid.Select(a => a.AidId));
    }

    [Fact]
    public async Task FlagEvidence_IsNull_ForAMissingFlag()
    {
        Assert.Null(await Repository.GetDealFlagEvidenceAsync(12345));
    }

    [Fact]
    public async Task RestoreFlaggedDeal_PutsTheDealBack_WithAFreshClock_AndResolvesItsFlags()
    {
        var (bob, sue, deal) = await SeedPairAsync();
        var cnToday = await Data.CnTodayAsync();
        // The first leg expired unaccepted, so the daily check cancels the deal and flags both accounts
        await Data.DealActivityAsync(deal, 1, BobNation, SueNation, cnToday.AddDays(-10), failureReason: "expired", failedOn: cnToday);
        await Data.ExecuteAsync("call vep_db.check_deal_inactivity()");
        var flags = await Repository.GetFlaggedDealsAsync();
        Assert.Equal(2, flags.Count);
        Assert.All(flags, f => Assert.True(f.FlagType == DealFlagType.AidFailed && f.CanRestoreDeal));
        // A cancelled deal keeps its state until the data loader's next update_deal_state run
        await Data.DealStateAsync(deal, "awaiting_send", nextSender: bob, nextReceiver: sue, responsible: bob,
            responsibleSince: cnToday.AddDays(-4));

        var result = await Repository.RestoreFlaggedDealAsync(flags[0].Id, "admin", " both were on vacation ");

        Assert.Equal(DealRestoreResult.Restored, result);
        Assert.Empty(await Repository.GetFlaggedDealsAsync());
        Assert.Equal(("active", null, null, null), (await Data.QueryAsync<(string, DateTime?, string?, string?)>(
            "select status, cancelled_on, cancelled_by, cancellation_reason from vep_db.deal where id = @deal", new { deal })).Single());
        Assert.Equal(0, await Data.ScalarAsync<int>("select count(*) from vep_db.deal_state where deal_id = @deal", new { deal }));
        Assert.All(await Data.QueryAsync<(string, string)>("select resolved_by, admin_notes from vep_db.deal_flag where deal_id = @deal", new { deal }),
            f => Assert.Equal(("admin", "Deal restored: both were on vacation"), f));

        // The excused failure doesn't cancel it again
        await Data.ExecuteAsync("call vep_db.check_deal_inactivity()");

        Assert.Equal("active", await Data.ScalarAsync<string>("select status from vep_db.deal where id = @deal", new { deal }));
        Assert.Empty(await Repository.GetFlaggedDealsAsync());
        var leg = (await Repository.GetDealFlagEvidenceAsync(flags[0].Id)).Legs.Single();
        Assert.Equal((DealLegOutcome.Expired, true), (leg.Outcome, leg.FailureExcused));
    }

    [Fact]
    public async Task RestoreFlaggedDeal_RefusesFlagsThatCantBeRestored()
    {
        var (bob, sue, deal, replacement) = await SeedCancelledByFlagAsync();
        var bobSlots = await Data.SlotIdsAsync(bob);
        var sueSlots = await Data.SlotIdsAsync(sue);
        var newDeal = await Data.DealAsync(bobSlots[1], sueSlots[1]);
        var warning = await Data.DealFlagAsync(newDeal, "1_day_inactive", bob, sue, "awaiting_send");
        var resolved = await Data.DealFlagAsync(deal, "5_day_replacement", bob, sue, "awaiting_send", resolvedOn: DateTime.UtcNow);

        Assert.Equal(DealRestoreResult.NotRestorable, await Repository.RestoreFlaggedDealAsync(warning, "admin", null));
        // Bob and Sue have a new deal together now
        Assert.Equal(DealRestoreResult.NotRestorable, await Repository.RestoreFlaggedDealAsync(replacement, "admin", null));
        Assert.Equal(DealRestoreResult.FlagNotFound, await Repository.RestoreFlaggedDealAsync(resolved, "admin", null));
        Assert.Equal(DealRestoreResult.FlagNotFound, await Repository.RestoreFlaggedDealAsync(12345, "admin", null));

        Assert.Equal("cancelled", await Data.ScalarAsync<string>("select status from vep_db.deal where id = @deal", new { deal }));
        Assert.Equal([replacement, warning], (await Repository.GetFlaggedDealsAsync()).Select(f => f.Id).Order());
    }

    [Fact]
    public async Task RestoreFlaggedDeal_NotesJustTheRestore_WithoutAdminNotes()
    {
        var (_, _, deal, flag) = await SeedCancelledByFlagAsync();

        Assert.Equal(DealRestoreResult.Restored, await Repository.RestoreFlaggedDealAsync(flag, "admin", "  "));

        Assert.Equal("Deal restored", await Data.ScalarAsync<string>("select admin_notes from vep_db.deal_flag where id = @flag", new { flag }));
        Assert.Equal("active", await Data.ScalarAsync<string>("select status from vep_db.deal where id = @deal", new { deal }));
    }

    /// <summary>
    /// Bob has held up his deal with Sue for the given days, and has been warned
    /// </summary>
    private async Task<(int Bob, int Sue, int Deal, int Warning, DateTime CnToday)> SeedHeldUpDealAsync(int daysHeldUp,
        string state = "awaiting_send", string? blockedReason = null)
    {
        var (bob, sue, deal) = await SeedPairAsync();
        var cnToday = await Data.CnTodayAsync();
        await Data.DealStateAsync(deal, state, nextSender: bob, nextReceiver: sue, responsible: bob,
            responsibleSince: cnToday.AddDays(-daysHeldUp), blockedReason: blockedReason);
        var warning = await Data.DealFlagAsync(deal, "1_day_inactive", bob, sue, blockedReason ?? "awaiting_send");
        return (bob, sue, deal, warning, cnToday);
    }

    private Task<string> DealStatusAsync(int deal) =>
        Data.ScalarAsync<string>("select status from vep_db.deal where id = @deal", new { deal });

    private Task<IEnumerable<(string Kind, DateTime? ExtendedUntil, string? EndReason)>> ExtensionsAsync(int deal) =>
        Data.QueryAsync<(string, DateTime?, string?)>(
            "select kind, extended_until, end_reason from vep_db.deal_extension where deal_id = @deal order by id", new { deal });

    [Fact]
    public async Task ExtendFlaggedDeal_FiveDays_MovesTheDeadline_AndClearsTheWarning()
    {
        // Cancelled at tomorrow's check without an extension
        var (bob, sue, deal, warning, cnToday) = await SeedHeldUpDealAsync(4);

        var result = await Repository.ExtendFlaggedDealAsync(warning, DealExtensionKind.FiveDays, "admin", " on vacation ");

        Assert.Equal(DealExtendResult.Extended, result);
        Assert.Equal($"Given 5 more days, until {cnToday.AddDays(6):yyyy-MM-dd}: on vacation",
            await Data.ScalarAsync<string>("select admin_notes from vep_db.deal_flag where id = @warning", new { warning }));
        Assert.Empty(await Repository.GetFlaggedDealsAsync());
        var listed = (await Repository.GetActiveDealsAsync(null, 25, 0)).Results.Single();
        Assert.Equal((cnToday.AddDays(6), DealExtensionKind.FiveDays, "admin", 4),
            (listed.CancelsOn!.Value, listed.ExtensionKind!.Value, listed.ExtendedBy, listed.DaysResponsible!.Value));

        // Tomorrow's check neither cancels the deal nor warns Bob again
        await Data.ExecuteAsync("call vep_db.check_deal_inactivity()");
        Assert.Equal("active", await DealStatusAsync(deal));
        Assert.Empty(await Repository.GetFlaggedDealsAsync());

        // A day past the old deadline Bob is warned again, as he would be a day into a normal clock
        await Data.ExecuteAsync("update vep_db.deal_extension set extended_until = @until where deal_id = @deal",
            new { deal, until = cnToday.AddDays(4) });
        await Data.ExecuteAsync("call vep_db.check_deal_inactivity()");
        Assert.Equal([bob], (await Repository.GetFlaggedDealsAsync()).Select(f => f.FlaggedAccount.AccountId));

        // And on the new deadline the deal is cancelled, ending the extension
        await Data.ExecuteAsync("update vep_db.deal_extension set extended_until = @until where deal_id = @deal",
            new { deal, until = cnToday });
        await Data.ExecuteAsync("call vep_db.check_deal_inactivity()");
        Assert.Equal("cancelled", await DealStatusAsync(deal));
        Assert.Contains(await Repository.GetFlaggedDealsAsync(), f => f.FlagType == DealFlagType.FiveDayReplacement && f.FlaggedAccount.AccountId == bob);
        Assert.Equal([("five_days", (DateTime?)cnToday, "deal_ended")],
            (await ExtensionsAsync(deal)).Select(e => (e.Kind, e.ExtendedUntil, e.EndReason)));
    }

    [Fact]
    public async Task ExtendFlaggedDeal_FiveDaysAgain_RunsFromTheExtendedDeadline()
    {
        var (bob, sue, deal, warning, cnToday) = await SeedHeldUpDealAsync(2);
        await Repository.ExtendFlaggedDealAsync(warning, DealExtensionKind.FiveDays, "admin", null);
        // Warned again later in the extension
        var second = await Data.DealFlagAsync(deal, "1_day_inactive", bob, sue, "awaiting_send");

        Assert.Equal(DealExtendResult.Extended, await Repository.ExtendFlaggedDealAsync(second, DealExtensionKind.FiveDays, "admin", null));

        Assert.Equal(cnToday.AddDays(13), (await Repository.GetActiveDealsAsync(null, 25, 0)).Results.Single().CancelsOn);
        Assert.Equal([("five_days", (string?)"superseded"), ("five_days", null)],
            (await ExtensionsAsync(deal)).Select(e => (e.Kind, e.EndReason)));
        Assert.Equal("Given 5 more days, until " + cnToday.AddDays(13).ToString("yyyy-MM-dd"),
            await Data.ScalarAsync<string>("select admin_notes from vep_db.deal_flag where id = @second", new { second }));
    }

    [Fact]
    public async Task ExtendFlaggedDeal_UntilNextLeg_HoldsOffForOutOfTurnAid_UntilSomeoneElseIsResponsible()
    {
        // Bob's out-of-turn aid has blocked the deal for a week, longer than the usual 5 days
        var (bob, sue, deal, warning, cnToday) = await SeedHeldUpDealAsync(7, "blocked", "out_of_sequence_aid");

        Assert.Equal(DealExtendResult.Extended,
            await Repository.ExtendFlaggedDealAsync(warning, DealExtensionKind.UntilNextLeg, "admin", null));

        var listed = (await Repository.GetActiveDealsAsync(null, 25, 0)).Results.Single();
        Assert.Equal((null, DealExtensionKind.UntilNextLeg), (listed.CancelsOn, listed.ExtensionKind!.Value));
        await Data.ExecuteAsync("call vep_db.check_deal_inactivity()");
        Assert.Equal("active", await DealStatusAsync(deal));
        Assert.Empty(await Repository.GetFlaggedDealsAsync());

        // The aid clears and it's Sue's turn: the extension ends, and Sue gets a normal clock
        await Data.ExecuteAsync(@"
            update vep_db.deal_state
            set state = 'awaiting_send', blocked_reason = null, next_sender_account_id = @sue, next_receiver_account_id = @bob,
                responsible_account_id = @sue, responsible_since = @today
            where deal_id = @deal", new { deal, bob, sue, today = cnToday });
        await Data.ExecuteAsync("call vep_db.check_deal_inactivity()");

        Assert.Equal("active", await DealStatusAsync(deal));
        Assert.Equal([("until_next_leg", "responsibility_moved")], (await ExtensionsAsync(deal)).Select(e => (e.Kind, e.EndReason)));
        Assert.Equal(cnToday.AddDays(5), (await Repository.GetActiveDealsAsync(null, 25, 0)).Results.Single().CancelsOn);
    }

    [Fact]
    public async Task ExtendFlaggedDeal_StopsApplying_OnceANewLegIsRecorded()
    {
        var (_, _, deal, warning, _) = await SeedHeldUpDealAsync(7);
        await Repository.ExtendFlaggedDealAsync(warning, DealExtensionKind.UntilNextLeg, "admin", null);

        // Bob is still responsible, but for the leg after the one he was given time for
        await Data.ExecuteAsync("update vep_db.deal_state set legs_counted = legs_counted + 1 where deal_id = @deal", new { deal });
        await Data.ExecuteAsync("call vep_db.check_deal_inactivity()");

        Assert.Equal("cancelled", await DealStatusAsync(deal));
        Assert.Equal([("until_next_leg", "deal_ended")], (await ExtensionsAsync(deal)).Select(e => (e.Kind, e.EndReason)));
    }

    [Fact]
    public async Task ExtendFlaggedDeal_RefusesFlagsWhoseDealCantBeExtended()
    {
        var (bob, sue, deal, warning, _) = await SeedHeldUpDealAsync(2);
        // Sue has become the one holding it up
        await Data.ExecuteAsync("update vep_db.deal_state set responsible_account_id = @sue where deal_id = @deal", new { deal, sue });
        var cancelledDeal = await Data.DealAsync((await Data.SlotIdsAsync(bob))[1], (await Data.SlotIdsAsync(sue))[1], "cancelled");
        var replacement = await Data.DealFlagAsync(cancelledDeal, "5_day_replacement", bob, sue, "awaiting_send");

        Assert.Equal(DealExtendResult.NotExtendable, await Repository.ExtendFlaggedDealAsync(warning, DealExtensionKind.FiveDays, "admin", null));
        Assert.Equal(DealExtendResult.NotExtendable, await Repository.ExtendFlaggedDealAsync(replacement, DealExtensionKind.FiveDays, "admin", null));
        Assert.Equal(DealExtendResult.FlagNotFound, await Repository.ExtendFlaggedDealAsync(12345, DealExtensionKind.FiveDays, "admin", null));

        Assert.Empty(await ExtensionsAsync(deal));
        Assert.Empty(await ExtensionsAsync(cancelledDeal));
        Assert.Equal(2, (await Repository.GetFlaggedDealsAsync()).Count);
    }

    [Fact]
    public async Task GetAccountSlots_ReturnsEverySlot_WithWhetherItHasADeal()
    {
        var (bob, _, _) = await SeedPairAsync();

        var response = await Repository.GetAccountSlotsAsync(bob);

        Assert.Equal(bob, response.AccountId);
        Assert.Equal(("Bob", "Valyria"), (response.Account.RulerName, response.Account.AllianceName));
        Assert.Equal([1, 2, 3, 4], response.Slots.Select(s => (int)s.SlotNumber));
        Assert.Equal([true, false, false, false], response.Slots.Select(s => s.HasActiveDeal));
        Assert.All(response.Slots, s => Assert.Same(response.Account, s.Account));
    }

    [Fact]
    public async Task GetAccountSlots_ReturnsNull_ForAMissingAccount()
    {
        Assert.Null(await Repository.GetAccountSlotsAsync(12345));
    }

    [Fact]
    public async Task GetAvailableSlots_LeavesOutSlotsInActiveDeals()
    {
        var (bob, sue, _) = await SeedPairAsync();
        await Data.DealAsync((await Data.SlotIdsAsync(bob))[1], (await Data.SlotIdsAsync(sue))[1], status: "cancelled");

        var available = await Repository.GetAvailableSlotsAsync(bob);

        Assert.Equal([2, 3, 4], available.Select(s => (int)s.SlotNumber));
    }

    [Fact]
    public async Task UpdateSlot_ChangesTheCycles()
    {
        var (bob, _, _) = await SeedPairAsync();
        var slotId = (await Data.SlotIdsAsync(bob))[0];

        var updated = await Repository.UpdateSlotAsync(new SlotUpdateRequest
        {
            SlotId = slotId, TechSentCycles = 1, TechReceivedCycles = 2, CashSentCycles = 3, CashReceivedCycles = 4
        }, "admin");

        Assert.True(updated);
        var slot = (await Repository.GetAccountSlotsAsync(bob)).Slots[0];
        Assert.Equal((1, 2, 3, 4), (slot.TechSentCycles, slot.TechReceivedCycles, slot.CashSentCycles, slot.CashReceivedCycles));
        Assert.Equal("admin", await Data.ScalarAsync<string>("select updated_by from slot where id = @slotId", new { slotId }));
    }

    [Fact]
    public async Task UpdateSlot_ReturnsFalse_ForAMissingSlot()
    {
        Assert.False(await Repository.UpdateSlotAsync(new SlotUpdateRequest { SlotId = 12345 }, "admin"));
    }
}
