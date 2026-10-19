using Valyria.Core.Entities;
using Valyria.Infrastructure.Repositories;

namespace Valyria.IntegrationTests;

public class MassMessagesRepositoryTests(MySqlFixture fixture) : RepositoryTestBase(fixture)
{
    private MassMessagesRepository Repository => new(Fixture.VepConnectionString);

    private async Task<(int First, int Second, int Deal)> PairAsync(int firstNation, string firstRuler, int secondNation, string secondRuler, int slot = 0)
    {
        var first = await Data.ScalarAsync<int?>("select id from account where nation_id = @firstNation", new { firstNation })
                    ?? await Data.MemberAsync(firstNation, firstRuler, 'B');
        var second = await Data.ScalarAsync<int?>("select id from account where nation_id = @secondNation", new { secondNation })
                     ?? await Data.MemberAsync(secondNation, secondRuler, 'S');
        var deal = await Data.DealAsync((await Data.SlotIdsAsync(first))[slot], (await Data.SlotIdsAsync(second))[slot]);
        return (first, second, deal);
    }

    [Fact]
    public async Task GetMassMessages_ListsTheFourMessages_WithNoRecipientsOrSends()
    {
        var summary = await Repository.GetMassMessagesAsync();

        Assert.Equal(
            [MassMessageCode.SendAid, MassMessageCode.AcceptAid, MassMessageCode.Overdue, MassMessageCode.FinalWarning],
            summary.Messages.Select(m => m.Code));
        Assert.All(summary.Messages, m =>
        {
            Assert.Empty(m.Recipients);
            Assert.Null(m.SentOn);
            Assert.Null(m.SentBy);
            Assert.False(string.IsNullOrWhiteSpace(m.Subject));
            Assert.False(string.IsNullOrWhiteSpace(m.Message));
        });
        Assert.Equal(await Data.CnTodayAsync(), summary.CnDate);
        Assert.Null(summary.DealStatesUpdatedOn);
    }

    [Fact]
    public async Task GetMassMessages_SendsEachAccountItsMostUrgentMessage()
    {
        var cnToday = await Data.CnTodayAsync();

        // Bob needs to send, and isn't flagged
        var (bob, sue, sendDeal) = await PairAsync(100, "Bob", 200, "Sue");
        await Data.DealStateAsync(sendDeal, "awaiting_send", bob, sue, responsible: bob, responsibleSince: cnToday);
        await Data.ExecuteAsync("update account set discord_id = 123456789012345678 where id = @bob", new { bob });

        // Dana has an offer to accept and a deal to send on; accepting wins
        var (carl, dana, acceptDeal) = await PairAsync(300, "Carl", 400, "Dana");
        await Data.AidAsync(901, 300, 400, cnToday, status: 1);
        await Data.DealActivityAsync(acceptDeal, 1, 300, 400, cnToday, aidId: 901);
        await Data.DealStateAsync(acceptDeal, "in_flight", dana, carl, inFlightAidId: 901, inFlightUntil: cnToday.AddDays(10));
        var (_, _, danaSendDeal) = await PairAsync(100, "Bob", 400, "Dana", slot: 1);
        await Data.DealStateAsync(danaSendDeal, "awaiting_send", dana, bob, responsible: dana, responsibleSince: cnToday);

        // A leg linked late counts as accepted, so Sue has nothing to accept
        var (_, _, lateDeal) = await PairAsync(300, "Carl", 200, "Sue", slot: 2);
        await Data.AidAsync(902, 300, 200, cnToday.AddDays(-12), status: 1);
        await Data.DealActivityAsync(lateDeal, 1, 300, 200, cnToday.AddDays(-12), aidId: 902, linkedLate: true);
        await Data.DealStateAsync(lateDeal, "in_flight", sue, carl, inFlightAidId: 902, inFlightUntil: cnToday);

        // Eve was warned 2 days into holding up a deal
        var (eve, fay, overdueDeal) = await PairAsync(500, "Eve", 600, "Fay");
        await Data.DealStateAsync(overdueDeal, "awaiting_send", eve, fay, responsible: eve, responsibleSince: cnToday.AddDays(-2));
        await Data.DealFlagAsync(overdueDeal, "1_day_inactive", eve, fay, "awaiting_send");

        // Gus's full slots have blocked a deal for 4 days, so it's cancelled at the next check
        var (gus, hank, finalDeal) = await PairAsync(700, "Gus", 800, "Hank");
        await Data.DealStateAsync(finalDeal, "blocked", hank, gus, responsible: gus, responsibleSince: cnToday.AddDays(-4), blockedReason: "receiver_slots_full");
        await Data.DealFlagAsync(finalDeal, "1_day_inactive", gus, hank, "receiver_slots_full");

        // Cancelled deals don't need anything
        var (ivy, _, cancelledDeal) = await PairAsync(900, "Ivy", 1000, "Jay");
        await Data.DealStateAsync(cancelledDeal, "awaiting_send", ivy, ivy, responsible: ivy, responsibleSince: cnToday);
        await Data.ExecuteAsync("update deal set status = 'cancelled' where id = @cancelledDeal", new { cancelledDeal });

        var summary = await Repository.GetMassMessagesAsync();
        var byCode = summary.Messages.ToDictionary(m => m.Code, m => m.Recipients);

        var bobRecipient = Assert.Single(byCode[MassMessageCode.SendAid]);
        Assert.Equal((bob, "Bob", "Bobland", "bob", 1, 0, 0, 0), (bobRecipient.AccountId, bobRecipient.RulerName, bobRecipient.NationName,
            bobRecipient.Discord, bobRecipient.SendCount, bobRecipient.AcceptCount, bobRecipient.BlockedCount, bobRecipient.FlaggedCount));
        Assert.Equal(123456789012345678, bobRecipient.DiscordUniqueId);
        Assert.Null(bobRecipient.DaysOverdue);

        var danaRecipient = Assert.Single(byCode[MassMessageCode.AcceptAid]);
        Assert.Equal((dana, 1, 1), (danaRecipient.AccountId, danaRecipient.SendCount, danaRecipient.AcceptCount));

        var eveRecipient = Assert.Single(byCode[MassMessageCode.Overdue]);
        Assert.Equal((eve, 1, 2, cnToday.AddDays(3)), (eveRecipient.AccountId, eveRecipient.FlaggedCount, eveRecipient.DaysOverdue!.Value, eveRecipient.CancelsOn!.Value));

        var gusRecipient = Assert.Single(byCode[MassMessageCode.FinalWarning]);
        Assert.Equal((gus, 1, 1, 4), (gusRecipient.AccountId, gusRecipient.BlockedCount, gusRecipient.FlaggedCount, gusRecipient.DaysOverdue!.Value));

        Assert.NotNull(summary.DealStatesUpdatedOn);
    }

    [Fact]
    public async Task SetMessageSent_ReturnsFalse_ForAMissingMessage()
    {
        var admin = await Data.MemberAsync(100, "Admin");

        Assert.False(await Repository.SetMessageSentAsync(12345, true, admin));
    }

    [Fact]
    public async Task SetMessageSent_RecordsTheFirstAdminToSendIt_UntilUnsent()
    {
        var first = await Data.MemberAsync(100, "First");
        var second = await Data.MemberAsync(200, "Second");
        var sendAid = (await Repository.GetMassMessagesAsync()).Messages.Single(m => m.Code == MassMessageCode.SendAid);

        Assert.True(await Repository.SetMessageSentAsync(sendAid.Id, true, first));
        Assert.True(await Repository.SetMessageSentAsync(sendAid.Id, true, second));

        var sent = (await Repository.GetMassMessagesAsync()).Messages.Single(m => m.Id == sendAid.Id);
        Assert.NotNull(sent.SentOn);
        Assert.Equal((first, "First", "Firstland", 100), (sent.SentBy!.AccountId, sent.SentBy.RulerName, sent.SentBy.NationName, sent.SentBy.NationId));
        Assert.All((await Repository.GetMassMessagesAsync()).Messages.Where(m => m.Id != sendAid.Id), m => Assert.Null(m.SentBy));

        Assert.True(await Repository.SetMessageSentAsync(sendAid.Id, false, second));

        var unsent = (await Repository.GetMassMessagesAsync()).Messages.Single(m => m.Id == sendAid.Id);
        Assert.Null(unsent.SentOn);
        Assert.Null(unsent.SentBy);
    }
}
