namespace Valyria.IntegrationTests;

/// <summary>
/// update_deal_state: who is held responsible when a deal can't move on
/// </summary>
public class DealStateTests(MySqlFixture fixture) : RepositoryTestBase(fixture)
{
    private const int BobNation = 100;
    private const int SueNation = 200;

    private record StateRow(string State, string? BlockedReason, int? ResponsibleAccountId, DateTime? ResponsibleSince);

    /// <summary>
    /// Bob (buyer, 4 slots, sends next) and Sue (seller) with a deal created 2 days ago that Bob has
    /// been responsible for since it was activated
    /// </summary>
    private async Task<(int Bob, int Sue, int Deal, DateTime CnToday)> SeedActivatedDealAsync()
    {
        var bob = await Data.MemberAsync(BobNation, "Bob", 'B');
        var sue = await Data.MemberAsync(SueNation, "Sue", 'S');
        var deal = await Data.DealAsync((await Data.SlotIdsAsync(bob))[0], (await Data.SlotIdsAsync(sue))[0],
            createdOn: DateTime.UtcNow.AddDays(-2));
        var cnToday = await Data.CnTodayAsync();
        await Data.DealStateAsync(deal, "awaiting_send", nextSender: bob, nextReceiver: sue, responsible: bob,
            responsibleSince: cnToday.AddDays(-2), stateSince: cnToday.AddDays(-2));
        await Data.ExecuteAsync("update vep_db.deal_state set activated_on = @activatedOn where deal_id = @deal",
            new { deal, activatedOn = cnToday.AddDays(-2) });
        return (bob, sue, deal, cnToday);
    }

    private async Task<StateRow> UpdateDealStateAsync(int deal)
    {
        await Data.ExecuteAsync("call vep_db.update_deal_state()");
        return (await Data.QueryAsync<StateRow>(@"
            select state as State, blocked_reason as BlockedReason, responsible_account_id as ResponsibleAccountId,
                responsible_since as ResponsibleSince
            from vep_db.deal_state where deal_id = @deal", new { deal })).Single();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SenderSlotsFull_BlamesTheSender_OnlyForNonDealAidSentAfterTheDeal(bool sentNonDealAidSinceDeal)
    {
        var (bob, _, deal, cnToday) = await SeedActivatedDealAsync();
        var bobSlots = await Data.SlotIdsAsync(bob);

        // Two slots hold legs of Bob's other deals
        var otherPartners = new[] { 300, 400 };
        for (var i = 0; i < otherPartners.Length; i++)
        {
            var partner = await Data.MemberAsync(otherPartners[i], $"Partner{i}", 'S');
            var otherDeal = await Data.DealAsync(bobSlots[i + 1], (await Data.SlotIdsAsync(partner))[0]);
            await Data.AidAsync(910 + i, BobNation, otherPartners[i], cnToday.AddDays(-1));
            await Data.DealActivityAsync(otherDeal, 1, BobNation, otherPartners[i], cnToday.AddDays(-1), aidId: 910 + i);
        }

        // The other two hold aid outside VEP, sent before the deal unless the test says otherwise
        await Data.AidAsync(920, BobNation, 500, cnToday.AddDays(-3), reason: "Financial Assistance");
        await Data.AidAsync(921, BobNation, 600, sentNonDealAidSinceDeal ? cnToday.AddDays(-1) : cnToday.AddDays(-3),
            reason: "Financial Assistance");

        var state = await UpdateDealStateAsync(deal);

        if (sentNonDealAidSinceDeal)
        {
            Assert.Equal(new StateRow("blocked", "sender_slots_full", bob, cnToday.AddDays(-2)), state);
        }
        else
        {
            Assert.Equal(new StateRow("pending_start", null, null, null), state);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AidBetweenThePair_IsOutOfSequence_OnlyWhenSentAfterTheDeal(bool sentAfterDeal)
    {
        var (_, sue, deal, cnToday) = await SeedActivatedDealAsync();
        await Data.AidAsync(930, SueNation, BobNation, sentAfterDeal ? cnToday.AddDays(-1) : cnToday.AddDays(-3),
            reason: "Financial Assistance");

        var state = await UpdateDealStateAsync(deal);

        if (sentAfterDeal)
        {
            Assert.Equal(new StateRow("blocked", "out_of_sequence_aid", sue, cnToday), state);
        }
        else
        {
            Assert.Equal(new StateRow("pending_start", null, null, null), state);
        }
    }

    [Fact]
    public async Task ExcusedSlots_RestartTheClock_OnceTheSenderHasAFreeSlot()
    {
        var (bob, _, deal, cnToday) = await SeedActivatedDealAsync();
        for (var i = 0; i < 4; i++)
            await Data.AidAsync(940 + i, BobNation, 500 + i, cnToday.AddDays(-3), reason: "Financial Assistance");

        Assert.Equal("pending_start", (await UpdateDealStateAsync(deal)).State);

        await Data.ExecuteAsync("delete from cybernations_db.aid where id = 940");

        Assert.Equal(new StateRow("awaiting_send", null, bob, cnToday), await UpdateDealStateAsync(deal));
    }

    private record LockRow(bool SlotsLocked, DateTime? SlotFreesOn, DateTime? CancelNotBefore, DateTime? CancelsOn);

    private async Task<LockRow> LockAsync(int deal) => (await Data.QueryAsync<LockRow>(@"
        select ds.slots_locked as SlotsLocked, ds.slot_frees_on as SlotFreesOn,
            ds.cancel_not_before as CancelNotBefore, dd.cancels_on as CancelsOn
        from vep_db.deal_state ds join vep_db.deal_deadline dd on dd.deal_id = ds.deal_id
        where ds.deal_id = @deal", new { deal })).Single();

    /// <summary>
    /// Bob's 4 slots fill with aid outside VEP sent since the deal, the oldest a day ago. pendingIds stay unaccepted
    /// </summary>
    private async Task FillBobsSlotsAsync(DateTime cnToday, params int[] pendingIds)
    {
        for (var i = 0; i < 4; i++)
            await Data.AidAsync(950 + i, BobNation, 500 + i, cnToday.AddDays(-1 + (i == 0 ? 0 : 1)),
                status: pendingIds.Contains(950 + i) ? 1 : 2, reason: "Financial Assistance");
    }

    [Fact]
    public async Task AcceptedOutsideAid_LocksTheSlots_AndHoldsCancellationUntilTheDayAfterOneFrees()
    {
        var (bob, _, deal, cnToday) = await SeedActivatedDealAsync();
        await FillBobsSlotsAsync(cnToday);

        Assert.Equal(new StateRow("blocked", "sender_slots_full", bob, cnToday.AddDays(-2)), await UpdateDealStateAsync(deal));
        // The oldest aid (a day ago) frees its slot 10 days after it was sent; the 5-day clock would end sooner
        Assert.Equal(new LockRow(true, cnToday.AddDays(9), cnToday.AddDays(10), cnToday.AddDays(10)), await LockAsync(deal));
    }

    [Fact]
    public async Task PendingOutsideAid_CanBeCancelled_SoTheNormalClockApplies()
    {
        var (bob, _, deal, cnToday) = await SeedActivatedDealAsync();
        await FillBobsSlotsAsync(cnToday, pendingIds: 952);

        Assert.Equal(new StateRow("blocked", "sender_slots_full", bob, cnToday.AddDays(-2)), await UpdateDealStateAsync(deal));
        Assert.Equal(new LockRow(false, cnToday.AddDays(9), null, cnToday.AddDays(3)), await LockAsync(deal));
    }

    [Fact]
    public async Task LockedSlots_StillWarn_ButDontCancelOnDayFive()
    {
        var (bob, _, deal, cnToday) = await SeedActivatedDealAsync();
        await Data.ExecuteAsync("update vep_db.deal_state set responsible_since = @since where deal_id = @deal",
            new { deal, since = cnToday.AddDays(-5) });
        await FillBobsSlotsAsync(cnToday);
        await UpdateDealStateAsync(deal);

        await Data.ExecuteAsync("call vep_db.check_deal_inactivity()");

        Assert.Equal("active", await Data.ScalarAsync<string>("select status from vep_db.deal where id = @deal", new { deal }));
        Assert.Equal(["1_day_inactive"], await Data.QueryAsync<string>(
            "select flag_type from vep_db.deal_flag where deal_id = @deal and flagged_account_id = @bob", new { deal, bob }));
    }

    [Fact]
    public async Task UnlockedSlots_AreCancelledOnDayFive()
    {
        var (_, _, deal, cnToday) = await SeedActivatedDealAsync();
        await Data.ExecuteAsync("update vep_db.deal_state set responsible_since = @since where deal_id = @deal",
            new { deal, since = cnToday.AddDays(-5) });
        await FillBobsSlotsAsync(cnToday, pendingIds: 953);
        await UpdateDealStateAsync(deal);

        await Data.ExecuteAsync("call vep_db.check_deal_inactivity()");

        Assert.Equal("cancelled", await Data.ScalarAsync<string>("select status from vep_db.deal where id = @deal", new { deal }));
    }

    [Fact]
    public async Task CancelDate_IsKept_AfterTheSlotFrees_AndIsntPushedBackByMoreOutsideAid()
    {
        var (bob, _, deal, cnToday) = await SeedActivatedDealAsync();
        await FillBobsSlotsAsync(cnToday);
        await UpdateDealStateAsync(deal);

        // A slot frees: Bob's turn to send, with the cancel date from the lock
        await Data.ExecuteAsync("delete from cybernations_db.aid where id = 950");
        Assert.Equal(new StateRow("awaiting_send", null, bob, cnToday.AddDays(-2)), await UpdateDealStateAsync(deal));
        Assert.Equal(new LockRow(false, null, cnToday.AddDays(10), cnToday.AddDays(10)), await LockAsync(deal));

        // Refilling the freed slot with more accepted outside aid doesn't buy more time
        await Data.AidAsync(960, BobNation, 700, cnToday, reason: "Financial Assistance");
        await UpdateDealStateAsync(deal);
        Assert.Equal(new LockRow(true, cnToday.AddDays(10), cnToday.AddDays(10), cnToday.AddDays(10)), await LockAsync(deal));
    }
}
