using Valyria.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using MySql.Data.MySqlClient;
using Valyria.Core.DTOs;
using Valyria.Core.Entities;

namespace Valyria.Infrastructure.Repositories;

public class MassMessagesRepository : IMassMessagesRepository
{
    private readonly string _connectionString;

    // Same CN date the deal procedures use
    private const string CnToday = "DATE(CONVERT_TZ(UTC_TIMESTAMP(), '+00:00', '-05:00'))";

    // A deal held up this many days is cancelled at the next daily check (5 days, see V10)
    private const int FinalWarningDaysOverdue = 4;

    private const string GetMassMessagesQuery = @"
select  mm.id as Id,
        case mm.code
            when 'send_aid' then 0
            when 'accept_aid' then 1
            when 'overdue' then 2
            when 'final_warning' then 3
        end as Code,
        mm.name as Name,
        mm.description as Description,
        mm.subject as Subject,
        mm.message as Message,
        mms.sent_on as SentOn,
        account.id as AccountId,
        nation.id as NationId,
        nation.ruler_name as RulerName,
        nation.nation_name as NationName
from mass_message mm
left join mass_message_sent mms
    on mms.mass_message_id = mm.id
    and mms.cn_date = " + CnToday + @"
left join account on mms.sent_by = account.id
left join cybernations_db.nation nation on account.nation_id = nation.id
order by mm.sort_order";

    // One row per account with something to do today, matching what the account page's
    // Action Needed card shows (getDealAction in the UI). Expecting aid isn't counted, since
    // there's nothing to do but keep a slot free
    private const string GetRecipientsQuery = @"
with deal_actions as (
    -- Send, free a slot, or wait out out-of-turn aid: whoever is holding up the deal
    select  ds.responsible_account_id as account_id,
            if(ds.state = 'awaiting_send', 'send', 'blocked') as kind,
            exists (
                select 1 from deal_flag df
                where df.deal_id = ds.deal_id
                    and df.flagged_account_id = ds.responsible_account_id
                    and df.flag_type = '1_day_inactive'
                    and df.resolved_on is null
            ) as flagged,
            datediff(" + CnToday + @", ds.responsible_since) as days_responsible,
            ds.responsible_since + interval 5 day as cancels_on
    from deal_state ds
    join deal d on d.id = ds.deal_id and d.status = 'active'
    where ds.responsible_account_id is not null

    union all

    -- Accept: a counted leg waiting on the receiver. Legs linked late count as accepted (see V9)
    select  ra.id,
            'accept',
            false,
            null,
            null
    from deal_state ds
    join deal_participants dp on dp.deal_id = ds.deal_id and dp.status = 'active'
    join deal_activity fl on fl.aid_id = ds.in_flight_aid_id
    join account ra
        on ra.nation_id = fl.expected_receiver_nation_id
        and ra.id in (dp.account_1_id, dp.account_2_id)
    where ds.state = 'in_flight'
        and fl.approved_on is null
        and not fl.linked_late
)
select  a.id as AccountId,
        n.id as NationId,
        n.ruler_name as RulerName,
        n.nation_name as NationName,
        al.name as AllianceName,
        a.discord as Discord,
        a.discord_id as DiscordUniqueId,
        sum(x.kind = 'send') as SendCount,
        sum(x.kind = 'accept') as AcceptCount,
        sum(x.kind = 'blocked') as BlockedCount,
        sum(x.flagged) as FlaggedCount,
        max(if(x.flagged, x.days_responsible, null)) as DaysOverdue,
        min(if(x.flagged, x.cancels_on, null)) as CancelsOn
from deal_actions x
join account a on a.id = x.account_id
join cybernations_db.nation n on a.nation_id = n.id
left join cybernations_db.alliance al on n.alliance_id = al.id
group by a.id, n.id, n.ruler_name, n.nation_name, al.name, a.discord, a.discord_id
order by n.ruler_name";

    public MassMessagesRepository(string connectionString) => _connectionString = connectionString;

    public async Task<MassMessagesSummary> GetMassMessagesAsync()
    {
        using var connection = new MySqlConnection(_connectionString);

        var messages = (await connection.QueryAsync<MassMessage, AccountSimplified, MassMessage>(
            GetMassMessagesQuery,
            (message, sentBy) =>
            {
                // No sent row today leaves every sent-by column null
                message.SentBy = message.SentOn == null ? null : sentBy;
                return message;
            },
            splitOn: "AccountId")).ToList();

        var recipients = await connection.QueryAsync<MassMessageRecipient>(GetRecipientsQuery);

        // Each account gets only the most urgent message that applies to it
        var recipientsByCode = recipients.ToLookup(GetMessageCode);
        foreach (var message in messages)
            message.Recipients = [.. recipientsByCode[message.Code]];

        return new MassMessagesSummary
        {
            CnDate = await connection.QuerySingleAsync<DateTime>($"select {CnToday}"),
            DealStatesUpdatedOn = await connection.QuerySingleOrDefaultAsync<DateTime?>(
                "select max(updated_on) from deal_state"),
            Messages = messages
        };
    }

    private static MassMessageCode GetMessageCode(MassMessageRecipient recipient)
    {
        if (recipient.FlaggedCount > 0)
        {
            return recipient.DaysOverdue >= FinalWarningDaysOverdue
                ? MassMessageCode.FinalWarning
                : MassMessageCode.Overdue;
        }

        // An offer that expires cancels the deal outright, so accepting comes before sending
        return recipient.AcceptCount > 0
            ? MassMessageCode.AcceptAid
            : MassMessageCode.SendAid;
    }

    public async Task<bool> SetMessageSentAsync(int messageId, bool sent, int sentByAccountId)
    {
        using var connection = new MySqlConnection(_connectionString);

        var exists = await connection.ExecuteScalarAsync<bool>(
            "select exists (select 1 from mass_message where id = @messageId)",
            new { messageId });

        if (!exists)
            return false;

        // Whoever marks it sent first keeps the credit if two admins toggle it at once
        var query = sent
            ? $@"
insert ignore into mass_message_sent (mass_message_id, cn_date, sent_by)
values (@messageId, {CnToday}, @sentByAccountId)"
            : $@"
delete from mass_message_sent
where mass_message_id = @messageId
    and cn_date = {CnToday}";

        await connection.ExecuteAsync(query, new { messageId, sentByAccountId });

        return true;
    }
}
