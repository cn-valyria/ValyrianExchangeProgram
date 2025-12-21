using Valyria.Core.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using MySql.Data.MySqlClient;
using Valyria.Core.DTOs;

namespace Valyria.Infrastructure.Repositories;

public class ListsRepository : IListsRepository
{
    private readonly string _connectionString;

    private const string GetAidListsQuery = @"
select  list.id as Id,
        name as Name,
        subject as Subject,
        message as Message,
        recipients_last_updated_on as RecipientsLastUpdatedOn,
        messages_sent_on as MessagesSentOn,
        account.id as AccountId,
        nation.id as NationId,
        nation.ruler_name as RulerName,
        nation.nation_name as NationName
from list
left join account on list.messages_sent_by = account.id
left join cybernations_db.nation on account.nation_id = nation.id";

    private const string GetListRecipientsQuery = @"
select  lr.list_id as ListId,
        a.id as AccountId,
        a.nation_id as NationId,
        nation_name as NationName,
        ruler_name as RulerName,
        alliance.name as AllianceName,
        slots_full - slots_used as SlotsFree,
        Credit,
        Debt,
        recent_activity as RecentActivity,
        discord as Discord,
        alp.days_until_removed as DaysOnCurrentList
from account a
join balance b on a.nation_id = b.nation_id
join aid_activity aa on a.id = aa.account_id
join cybernations_db.nation nation on a.nation_id = nation.id
join cybernations_db.alliance alliance on nation.alliance_id = alliance.id
join list_recipient lr on a.id = lr.account_id
join account_list_performance alp 
    on lr.account_id = alp.account_id
    and lr.list_id = alp.list_id
    and date_add(alp.day_started, interval (alp.days_until_removed + 1) day) >= curdate()
order by lr.list_id, lr.priority;";

    public ListsRepository(string connectionString) => _connectionString = connectionString;

    public async Task<List<AidListDetail>> GetAidListsAsync()
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        
        var lists = await sqlConnection.QueryAsync<AidListDetail, SimplifiedAccount, AidListDetail>(
            GetAidListsQuery,
            (aidList, account) =>
            {
                aidList.MessagesSentBy = account;
                return aidList;
            },
            splitOn: "AccountId");

        var recipients = await sqlConnection.QueryAsync<ListRecipientDetail>(GetListRecipientsQuery);

        foreach (var list in lists)
            list.Recipients = recipients.Where(r => r.ListId == list.Id).ToList();

        return [.. lists];
    }

    public async Task<AidListDetail> GetAidListAsync(int id)
    {
        using var sqlConnection = new MySqlConnection(_connectionString);

        var list = (await sqlConnection.QueryAsync<AidListDetail, SimplifiedAccount, AidListDetail>(
            $"{GetAidListsQuery} where list.id = @id",
            (aidList, account) =>
            {
                aidList.MessagesSentBy = account;
                return aidList;
            },
            new { id },
            splitOn: "AccountId"
        )).First();
        list.Recipients = [.. await sqlConnection.QueryAsync<ListRecipientDetail>(GetListRecipientsQuery)];

        return list;
    }

    public async Task UpdateListAsync(int listId, DateTime? messagesSentOn, int? messagesSentByAccountId)
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        await sqlConnection.OpenAsync();

        const string query = @"
update list
set messages_sent_on = @messagesSentOn,
    messages_sent_by = @messagesSentByAccountId
where id = @listId";

        await sqlConnection.ExecuteAsync(query, new { listId, messagesSentOn, messagesSentByAccountId });
    }

    public async Task<List<ListRecipientDetail>> GetListRecipientsForAccountAsync(int accountId)
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        var allRecipients = await sqlConnection.QueryAsync<ListRecipientDetail>(GetListRecipientsQuery);

        var sendingCash = allRecipients.Where(r => r.ListId == 1);
        var receivingCash = allRecipients.Where(r => r.ListId == 2);
        var sendingTech = allRecipients.Where(r => r.ListId == 3);
        var receivingTech = allRecipients.Where(r => r.ListId == 4);

        // Potential transactions are always the inverse of whichever list the current account is in, 
        // i.e. an account that is sending cash has potential transactions with nations that are receiving cash
        if (sendingCash.Any(r => r.AccountId == accountId)) return receivingCash.ToList();
        if (receivingCash.Any(r => r.AccountId == accountId)) return sendingCash.ToList();
        if (sendingTech.Any(r => r.AccountId == accountId)) return receivingTech.ToList();
        if (receivingTech.Any(r => r.AccountId == accountId)) return sendingTech.ToList();

        // Safe exit if we didn't find the account anywhere for some reason
        return new List<ListRecipientDetail>();
    }
}