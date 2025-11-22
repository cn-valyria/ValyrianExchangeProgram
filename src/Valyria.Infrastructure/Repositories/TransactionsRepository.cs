using Valyria.Core.Enums;
using Valyria.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using MySql.Data.MySqlClient;
using Valyria.Core.DTOs;
using Valyria.Infrastructure.Exceptions;

namespace Valyria.Infrastructure.Repositories;

public class TransactionsRepository : ITransactionsRepository
{
    private readonly string _connectionString;

    public TransactionsRepository(string connectionString) => _connectionString = connectionString;

    public async Task<DataCollection<TransactionDetail>> SearchTransactionsAsync(
        TransactionType transactionType,
        TransactionFilters filters,
        int limit,
        int offset)
    {
        using var sqlConnection = new MySqlConnection(_connectionString);

        await sqlConnection.ExecuteAsync("search_transactions", new 
        {
            _type = transactionType,
            _account_id = (int?) null,
            _sent_by = filters.SentBy,
            _received_by = filters.ReceivedBy,
            _sent_since = filters.SentSince,
            _sent_until = filters.SentUntil
        }, commandType: CommandType.StoredProcedure);

        var totalCount = await sqlConnection.QueryFirstAsync<int>("select count(1) from tmpTxnSearchResults");
        var results = await sqlConnection.QueryAsync<TransactionDetail>(
            "select * from tmpTxnSearchResults order by AidId desc, StartsOn desc limit @offset, @limit",
            new { offset, limit });

        return new DataCollection<TransactionDetail>
        {
            TotalCount = totalCount,
            Results = results
        };
    }

    public async Task<AccountTransactionsResponse> GetTransactionsByAccount(int accountId, int limit, int offset)
    {
        using var sqlConnection = new MySqlConnection(_connectionString);

        await sqlConnection.ExecuteAsync("search_transactions", new
        {
            _type = (int?) null,
            _account_id = accountId,
            _sent_by = (string?) null,
            _received_by = (string?) null,
            _sent_since = (DateTime?) null,
            _sent_until = (DateTime?) null
        }, commandType: CommandType.StoredProcedure);

        var totalCount = await sqlConnection.QueryFirstAsync<int>("select count(1) from tmpTxnSearchResults");
        var results = await sqlConnection.QueryAsync<TransactionDetail>(
            "select * from tmpTxnSearchResults order by AidId desc, StartsOn desc limit @offset, @limit",
            new { offset, limit });

        var (TotalCredit, TotalDebt) = await sqlConnection.QueryFirstAsync<(int, int)>(@"
select 
    coalesce(sum(case when AidId is null and SentByNationId is not null or sent_account.id = @accountId then 1 else 0 end), 0) as TotalCredit,
    coalesce(sum(case when AidId is null and SentByNationId is null or recd_account.id = @accountId then 1 else 0 end), 0) as TotalDebt
from tmpTxnSearchResults results
left join account sent_account on results.SentByNationId = sent_account.nation_id
left join account recd_account on results.ReceivedByNationId = recd_account.nation_id", new { accountId });

        return new AccountTransactionsResponse
        {
            CurrentPage = new DataCollection<TransactionDetail>
            {
                TotalCount = totalCount,
                Results = results
            },
            TotalCreditTxns = TotalCredit,
            TotalDebtTxns = TotalDebt
        };
    }

    public async Task<int> CreateTransactionAsync(TransactionCreateRequest transaction)
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        const string createTransactionQuery = @"
insert into transaction (
    aid_id,
    sending_nation_id,
    receiving_nation_id,
    reason_override,
    lu,
    co,
    rate,
    ct,
    cc,
    tc,
    tt
)
values (@aid_id, @sending_nation_id, @receiving_nation_id, @reason_override, @lu, @co, @rate, @ct, @cc, @tc, @tt)";
        
        await sqlConnection.ExecuteAsync(createTransactionQuery, new
        {
            aid_id = transaction.AidId,
            sending_nation_id = transaction.SendingNationId,
            receiving_nation_id = transaction.ReceivingNationId,
            reason_override = transaction.ReasonOverride,
            lu = transaction.Lu,
            co = transaction.Classification,
            rate = transaction.Rate,
            ct = transaction.CashMovedTechCredit,
            cc = transaction.CashMovedCashCredit,
            tc = transaction.TechMovedCashCredit,
            tt = transaction.TechMovedTechCredit
        });

        const string getTransactionIdQuery = @"
select id 
from transaction
where (aid_id is null and @aid_id is null or aid_id = @aid_id)
and (sending_nation_id is null and @sending_nation_id is null or sending_nation_id = @sending_nation_id)
and (receiving_nation_id is null and @receiving_nation_id is null or receiving_nation_id = @receiving_nation_id)";

        return await sqlConnection.QueryFirstAsync<int>(getTransactionIdQuery, new
        {
            aid_id = transaction.AidId,
            sending_nation_id = transaction.SendingNationId,
            receiving_nation_id = transaction.ReceivingNationId,
        });
    }

    public async Task UpdateTransactionAsync(TransactionUpdateRequest transaction)
    {
        if (!await TransactionExists(transaction.Id))
            throw new NotFoundException($"Unable to find a transaction to update by ID {transaction.Id}");

        using var sqlConnection = new MySqlConnection(_connectionString);
        const string query = @"
update transaction
set
    reason_override = @reason_override,
    lu = @lu,
    co = @co,
    rate = @rate,
    ct = @ct,
    cc = @cc,
    tc = @tc,
    tt = @tt
where id = @id";

        await sqlConnection.ExecuteAsync(query, new
        {
            id = transaction.Id,
            reason_override = transaction.ReasonOverride,
            lu = transaction.Lu,
            co = transaction.Classification,
            rate = transaction.Rate,
            ct = transaction.CashMovedTechCredit,
            cc = transaction.CashMovedCashCredit,
            tc = transaction.TechMovedCashCredit,
            tt = transaction.TechMovedTechCredit
        });
    }

    public async Task DeleteTransactionAsync(int transactionId)
    {
        if (!await TransactionExists(transactionId))
            throw new NotFoundException($"Unable to find a transaction to delete by ID {transactionId}");

        using var sqlConnection = new MySqlConnection(_connectionString);
        await sqlConnection.ExecuteAsync(
            "delete from transaction where id = @id",
            new { id = transactionId });
    }

    private async Task<bool> TransactionExists(int transactionId)
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        var transactionExists = await sqlConnection.QueryFirstAsync<bool?>(
            "select 1 from transaction where id = @id limit 1",
            new { id = transactionId });
        return transactionExists ?? false;
    }
}