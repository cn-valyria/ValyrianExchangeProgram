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
}
