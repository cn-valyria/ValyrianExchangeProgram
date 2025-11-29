using Dapper;
using MySql.Data.MySqlClient;
using System.Data;
using Valyria.Core.Interfaces;

namespace Valyria.Infrastructure.Repositories;

public class DataLoaderRepository : IDataLoaderRepository
{
    private readonly string _connectionString;

    public DataLoaderRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<int> LoadTodaysTransactionsAsync()
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        return await sqlConnection.ExecuteAsync("load_todays_transactions", commandType: CommandType.StoredProcedure);
    }

    public async Task LoadTodaysListsAsync()
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        await sqlConnection.ExecuteAsync("load_todays_lists", commandType: CommandType.StoredProcedure);
    }
}
