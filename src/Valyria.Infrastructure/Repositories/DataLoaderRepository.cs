using Dapper;
using MySql.Data.MySqlClient;
using System.Data;
using System.Text;
using Valyria.Core.Interfaces;
using Valyria.Core.DTOs;
using Valyria.Core.DTOs.CN;

namespace Valyria.Infrastructure.Repositories;

public class DataLoaderRepository : IDataLoaderRepository
{
    // The deal procedures scan every slot pair, so they can run well past Dapper's 30 second default
    private const int DealProcedureTimeoutSeconds = 600;

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

    public async Task<DealStateResult> UpdateDealStateAsync(bool checkInactivity)
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        await sqlConnection.OpenAsync();

        // Link newly imported aid to deals as legs, and record approved/failed offers
        var activityTracked = await sqlConnection.ExecuteAsync(
            "track_deal_activity",
            commandType: CommandType.StoredProcedure,
            commandTimeout: DealProcedureTimeoutSeconds);

        // Work out whose turn each deal is, and who (if anyone) is holding it up
        await sqlConnection.ExecuteAsync(
            "update_deal_state",
            commandType: CommandType.StoredProcedure,
            commandTimeout: DealProcedureTimeoutSeconds);

        // Flag accounts holding up deals, and cancel any past the replacement threshold or with a
        // failed offer. The check counts days against today, so it must only run once today's aid
        // is loaded, otherwise runs early in the day judge deals on yesterday's data
        if (checkInactivity)
            await sqlConnection.ExecuteAsync(
                "check_deal_inactivity",
                commandType: CommandType.StoredProcedure,
                commandTimeout: DealProcedureTimeoutSeconds);

        // Put accounts whose nation was deleted on hold, cancelling their deals, so they aren't
        // matched and their partners can be rematched
        var holdParameters = new DynamicParameters();
        holdParameters.Add("p_accounts_held", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await sqlConnection.ExecuteAsync(
            "hold_deleted_nation_accounts",
            holdParameters,
            commandType: CommandType.StoredProcedure,
            commandTimeout: DealProcedureTimeoutSeconds);

        // Add and remove team bonus slots as events start and end and nations change teams, cancelling deals
        // on bonus slots that have ended. Runs after the hold so deleted nations' deals keep that reason
        var teamEventParameters = new DynamicParameters();
        teamEventParameters.Add("p_deals_cancelled", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await sqlConnection.ExecuteAsync(
            "sync_team_event_slots",
            teamEventParameters,
            commandType: CommandType.StoredProcedure,
            commandTimeout: DealProcedureTimeoutSeconds);

        return new DealStateResult(activityTracked, holdParameters.Get<int>("p_accounts_held"),
            teamEventParameters.Get<int>("p_deals_cancelled"));
    }

    public async Task<DealMatchingResult> MatchDealsAsync()
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        await sqlConnection.OpenAsync();

        // Anything above this id was created by the match run below
        var lastDealIdBeforeMatch = await sqlConnection.ExecuteScalarAsync<int>("select coalesce(max(id), 0) from deal");

        // Match new deals for accounts with available slots. match_deals_run builds
        // the wait queue and candidate pairs once, then loops internally until no
        // more matches can be made (capped at 500 iterations inside the proc)
        var parameters = new DynamicParameters();
        parameters.Add("p_deals_created", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await sqlConnection.ExecuteAsync(
            "match_deals_run",
            parameters,
            commandType: CommandType.StoredProcedure,
            commandTimeout: DealProcedureTimeoutSeconds);

        // Drop cancelled deals from deal_state and add the newly matched ones
        await sqlConnection.ExecuteAsync(
            "update_deal_state",
            commandType: CommandType.StoredProcedure,
            commandTimeout: DealProcedureTimeoutSeconds);

        // Discord IDs of everyone who picked up a new partner and hasn't opted out, for the notification
        const string newDealDiscordIdsQuery = @"
select distinct a.discord_id
from deal d
join slot s on s.id in (d.slot_1_id, d.slot_2_id)
join account a on s.account_id = a.id
where d.id > @lastDealIdBeforeMatch
    and d.status = 'active'
    and a.discord_id <> 0
    and a.discord_notifications_enabled";

        var discordIds = await sqlConnection.QueryAsync<long>(newDealDiscordIdsQuery, new { lastDealIdBeforeMatch });

        return new DealMatchingResult(parameters.Get<int>("p_deals_created"), [.. discordIds]);
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
