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
