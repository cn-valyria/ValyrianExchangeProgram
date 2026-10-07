using Dapper;
using MySql.Data.MySqlClient;
using System.Data;
using System.Text;
using Valyria.Core.Interfaces;
using Valyria.Core.DTOs.CN;

namespace Valyria.Infrastructure.Repositories;

public class DataLoaderRepository : IDataLoaderRepository
{
    private readonly string _connectionString;

    public DataLoaderRepository(string connectionString)
    {
        _connectionString = connectionString;
    }


    public async Task<bool> IsFileAlreadyImported(string fileName)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        var tempTable = fileName switch
        {
            var name when name.Contains("_Nation_") => "todays_file_nation",
            var name when name.Contains("_Alliance_") => "todays_file_alliance",
            var name when name.Contains("_Aid_") => "todays_file_aid",
            var name when name.Contains("_War_") => "todays_file_war",
            _ => throw new InvalidDataException($"File name {fileName} doesn't map to a known temp table.")
        };
        var currentFileName = await connection.QuerySingleOrDefaultAsync<string>($"select distinct(file_name) from {tempTable}");
        return currentFileName is not null && $"{fileName}.txt" == currentFileName;
    }

    public async Task<int> ImportAlliancesAsync(IReadOnlyCollection<Alliance> data, string fileName)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        // Insert into temp table
        await connection.ExecuteAsync("TRUNCATE TABLE todays_file_alliance;");
        var rowsAffected = await UpdateTempTable(
            connection,
            "todays_file_alliance",
            data,
            [
                "alliance_id", "name", "updated", "total_nations", "active_nations", "percent_active",
                "strength", "average_strength", "score", "land", "infrastructure", "technology", "war",
                "peace", "soldiers", "tanks", "cruise", "nukes", "aircraft", "navy", "anarchy", "file_name"
            ],
            (alliance) =>
            [
                alliance.Id, alliance.Name, alliance.Updated, alliance.TotalNations,
                alliance.ActiveNations, alliance.PercentActive, alliance.Strength, alliance.AverageStrength,
                alliance.Score, alliance.Land, alliance.Infrastructure, alliance.Technology,
                alliance.War, alliance.Peace, alliance.Soldiers, alliance.Tanks, alliance.Cruise,
                alliance.Nukes, alliance.Aircraft, alliance.Navy, alliance.Anarchy,
                $"{fileName}.txt"
            ]);

        // Execute stored procedure to merge data
        await connection.ExecuteAsync("update_alliance_data", commandType: CommandType.StoredProcedure);
        return rowsAffected;
    }

    public async Task<int> ImportNationsAsync(IReadOnlyCollection<Nation> data, string fileName)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        // Insert into temp table
        await connection.ExecuteAsync("TRUNCATE TABLE todays_file_nation;");
        var rowsAffected = await UpdateTempTable(
            connection,
            "todays_file_nation",
            data,
            [
                "nation_id", "ruler_name", "nation_name", "alliance_id", "alliance_date", "alliance_status",
                "government_type", "religion", "team", "created", "technology", "infrastructure", "base_land",
                "war_status", "resource_1", "resource_2", "votes", "strength", "defcon", "base_soldiers",
                "tanks", "cruise_missiles", "nukes", "recent_activity", "connected_resource_1",
                "connected_resource_2", "connected_resource_3", "connected_resource_4",
                "connected_resource_5", "connected_resource_6", "connected_resource_7",
                "connected_resource_8", "connected_resource_9", "connected_resource_10", "file_name"
            ],
            (nation) =>
            [
                nation.NationId, nation.RulerName, nation.NationName, nation.AllianceId,
                nation.AllianceDate, nation.AllianceStatus, (int)nation.GovernmentType,
                (int)nation.Religion, (int)nation.Team, nation.Created, nation.Technology, nation.Infrastructure,
                nation.BaseLand, (int)nation.WarStatus, nation.Resource1, nation.Resource2,
                nation.Votes, nation.Strength, nation.Defcon, nation.BaseSoldiers, nation.Tanks, nation.CruiseMissiles,
                nation.Nukes, (int)nation.RecentActivity, nation.ConnectedResource1,
                nation.ConnectedResource2, nation.ConnectedResource3,
                nation.ConnectedResource4, nation.ConnectedResource5,
                nation.ConnectedResource6, nation.ConnectedResource7,
                nation.ConnectedResource8, nation.ConnectedResource9,
                nation.ConnectedResource10, $"{fileName}.txt"
            ]);

        // Execute stored procedure to merge data
        await connection.ExecuteAsync("update_nation_data", commandType: CommandType.StoredProcedure);
        return rowsAffected;
    }

    public async Task<int> ImportAidAsync(IReadOnlyCollection<Aid> data, string fileName)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        // Insert into temp table
        await connection.ExecuteAsync("TRUNCATE TABLE todays_file_aid;");
        var rowsAffected = await UpdateTempTable(
            connection,
            "todays_file_aid",
            data,
            [
                "aid_id", "sending_nation_id", "sending_alliance_id", "sending_team", "receiving_nation_id",
                "receiving_alliance_id", "receiving_team", "status", "money", "technology", "soldiers", "date",
                "reason", "file_name"
            ],
            (aid) =>
            [
                aid.Id, aid.SendingNationId, aid.SendingAllianceId, (int)aid.SendingTeam, aid.ReceivingNationId,
                aid.ReceivingAllianceId, (int)aid.ReceivingTeam, (int)aid.Status, aid.Money, aid.Technology,
                aid.Soldiers, aid.Date, aid.Reason, $"{fileName}.txt"
            ]);

        // Execute stored procedure to merge data
        await connection.ExecuteAsync("update_aid_data", commandType: CommandType.StoredProcedure);

        // Update expired aid records
        await connection.ExecuteAsync(@"
            UPDATE aid
            SET status = 4
            WHERE date < CURDATE() - INTERVAL 10 DAY
            AND status <> 4");

        return rowsAffected;
    }

    public async Task<int> ImportWarsAsync(IReadOnlyCollection<War> data, string fileName)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        // Insert into temp table
        await connection.ExecuteAsync("TRUNCATE TABLE todays_file_war;");
        var rowsAffected = await UpdateTempTable(
            connection,
            "todays_file_war",
            data,
            [
                "war_id", "attacking_nation_id", "attacking_alliance_id", "attacking_team",
                "defending_nation_id", "defending_alliance_id", "defending_team", "war_status",
                "begin_date", "end_date", "reason", "destruction", "attack_percent", "defend_percent",
                "file_name"
            ],
            (war) =>
            [
                war.Id, war.AttackingNationId, war.AttackingAllianceId, (int)war.AttackingTeam,
                war.DefendingNationId, war.DefendingAllianceId, (int)war.DefendingTeam, (int)war.WarStatus,
                war.BeginDate, war.EndDate, war.Reason, war.Destruction,
                war.AttackPercent, war.DefendPercent, $"{fileName}.txt"
            ]);

        // Execute stored procedure to merge data
        await connection.ExecuteAsync("update_war_data", commandType: CommandType.StoredProcedure);
        return rowsAffected;
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
        await sqlConnection.ExecuteAsync("update list set recipients_last_updated_on = now();");
    }

    public async Task ProcessDealsAsync()
    {
        using var sqlConnection = new MySqlConnection(_connectionString);

        // Track deal activity from newly imported aid
        await sqlConnection.ExecuteAsync("track_deal_activity", commandType: CommandType.StoredProcedure);

        // Check for inactive deals and flag them
        await sqlConnection.ExecuteAsync("check_deal_inactivity", commandType: CommandType.StoredProcedure);

        // Match new deals for accounts with available slots. match_deals_run builds
        // the wait queue and candidate pairs once, then loops internally until no
        // more matches can be made (capped at 500 iterations inside the proc)
        var parameters = new DynamicParameters();
        parameters.Add("p_deals_created", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await sqlConnection.ExecuteAsync(
            "match_deals_run",
            parameters,
            commandType: CommandType.StoredProcedure);
    }

    public async Task RemoveAccountsWithFullSlotsFromListsAsync()
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        const string sql = @"
delete from list_recipient where account_id in (
	select account_id
    from aid_activity
	where slots_used = slots_full
);";

        await sqlConnection.ExecuteAsync(sql);
        await sqlConnection.ExecuteAsync("update list set recipients_last_updated_on = now();");
    }

    private async Task<int> UpdateTempTable<T>(
        MySqlConnection connection,
        string tableName,
        IReadOnlyCollection<T> data,
        string[] columns,
        Func<T, object?[]> generateEscapedData)
    {
        const int batchSize = 1000; // MySQL can handle large batches, adjust if needed
        var totalRowsAffected = 0;

        // Build the column list for the INSERT statement
        var columnList = string.Join(", ", columns);

        // Process data in batches
        var batches = data
            .Select((item, index) => new { item, index })
            .GroupBy(x => x.index / batchSize)
            .Select(g => g.Select(x => x.item).ToList());

        using var transaction = await connection.BeginTransactionAsync();
        try
        {
            foreach (var batch in batches)
            {
                var sql = new StringBuilder();
                sql.AppendLine($"INSERT INTO {tableName}");
                sql.AppendLine($"    ({columnList})");
                sql.AppendLine("VALUES");

                var parameters = new DynamicParameters();
                var valuesClauses = new List<string>();

                for (var i = 0; i < batch.Count; i++)
                {
                    var rowData = generateEscapedData(batch[i]);
                    var paramNames = new List<string>();

                    for (var j = 0; j < rowData.Length; j++)
                    {
                        var paramName = $"p{i}_{j}";
                        paramNames.Add($"@{paramName}");
                        parameters.Add(paramName, rowData[j]);
                    }

                    valuesClauses.Add($"    ({string.Join(", ", paramNames)})");
                }

                sql.Append(string.Join(",\n", valuesClauses));

                var rowsAffected = await connection.ExecuteAsync(sql.ToString(), parameters, transaction);
                totalRowsAffected += rowsAffected;
            }

            await transaction.CommitAsync();
            return totalRowsAffected;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
