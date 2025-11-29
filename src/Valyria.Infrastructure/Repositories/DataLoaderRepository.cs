using Dapper;
using MySql.Data.MySqlClient;
using System.Data;
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
        var currentFileName = await connection.QuerySingleAsync<string>($"select distinct(file_name) from {tempTable}");
        return $"{fileName}.txt" == currentFileName;
    }

    public async Task<int> ImportAlliancesAsync(IReadOnlyCollection<Alliance> data, string fileName)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        const string insertTempDataCommand = @"
INSERT INTO todays_file_alliance
    (alliance_id, name, updated, total_nations, active_nations, percent_active, 
    strength, average_strength, score, land, infrastructure, technology, war, 
    peace, soldiers, tanks, cruise, nukes, aircraft, navy, anarchy, file_name)
VALUES
    (@AllianceId, @Name, @Updated, @TotalNations, @ActiveNations, @PercentActive,
    @Strength, @AverageStrength, @Score, @Land, @Infrastructure, @Technology, 
    @War, @Peace, @Soldiers, @Tanks, @Cruise, @Nukes, @Aircraft, @Navy,
    @Anarchy, @FileName)";

        // Insert into temp table
        await connection.ExecuteAsync("TRUNCATE TABLE todays_file_alliance;");
        var rowsAffected = await connection.ExecuteAsync(
            insertTempDataCommand,
            data.Select(alliance => new
            {
                AllianceId = alliance.Id,
                alliance.Name,
                alliance.Updated,
                alliance.TotalNations,
                alliance.ActiveNations,
                alliance.PercentActive,
                alliance.Strength,
                alliance.AverageStrength,
                alliance.Score,
                alliance.Land,
                alliance.Infrastructure,
                alliance.Technology,
                alliance.War,
                alliance.Peace,
                alliance.Soldiers,
                alliance.Tanks,
                alliance.Cruise,
                alliance.Nukes,
                alliance.Aircraft,
                alliance.Navy,
                alliance.Anarchy,
                FileName = $"{fileName}.txt"
            }));

        // Execute stored procedure to merge data
        await connection.ExecuteAsync("update_alliance_data", commandType: CommandType.StoredProcedure);
        return rowsAffected;
    }

    public async Task<int> ImportNationsAsync(IReadOnlyCollection<Nation> data, string fileName)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        const string insertTempDataCommand = @"
INSERT INTO todays_file_nation
    (nation_id, ruler_name, nation_name, alliance_id, alliance_date, alliance_status, 
    government_type, religion, team, created, technology, infrastructure, base_land, 
    war_status, resource_1, resource_2, votes, strength, defcon, base_soldiers, 
    tanks, cruise_missiles, nukes, recent_activity, connected_resource_1, 
    connected_resource_2, connected_resource_3, connected_resource_4, 
    connected_resource_5, connected_resource_6, connected_resource_7,
    connected_resource_8, connected_resource_9, connected_resource_10, file_name)
VALUES
    (@NationId, @RulerName, @NationName, @AllianceId, @AllianceDate, @AllianceStatus, 
    @GovernmentType, @Religion, @Team, @Created, @Technology, @Infrastructure,
    @BaseLand, @WarStatus, @Resource1, @Resource2, @Votes, @Strength, @Defcon,
    @BaseSoldiers, @Tanks, @CruiseMissiles, @Nukes, @RecentActivity,
    @ConnectedResource1, @ConnectedResource2, @ConnectedResource3, @ConnectedResource4,
    @ConnectedResource5, @ConnectedResource6, @ConnectedResource7, @ConnectedResource8,
    @ConnectedResource9, @ConnectedResource10, @FileName)";

        // Insert into temp table
        await connection.ExecuteAsync("TRUNCATE TABLE todays_file_nation;");
        var rowsAffected = await connection.ExecuteAsync(
            insertTempDataCommand,
            data.Select(nation => new
            {
                nation.NationId,
                nation.RulerName,
                nation.NationName,
                nation.AllianceId,
                nation.AllianceDate,
                nation.AllianceStatus,
                GovernmentType = (int) nation.GovernmentType,
                Religion = (int) nation.Religion,
                Team = (int) nation.Team,
                nation.Created,
                nation.Technology,
                nation.Infrastructure,
                nation.BaseLand,
                WarStatus = (int) nation.WarStatus,
                nation.Resource1,
                nation.Resource2,
                nation.Votes,
                nation.Strength,
                nation.Defcon,
                nation.BaseSoldiers,
                nation.Tanks,
                nation.CruiseMissiles,
                nation.Nukes,
                RecentActivity = (int) nation.RecentActivity,
                nation.ConnectedResource1,
                nation.ConnectedResource2,
                nation.ConnectedResource3,
                nation.ConnectedResource4,
                nation.ConnectedResource5,
                nation.ConnectedResource6,
                nation.ConnectedResource7,
                nation.ConnectedResource8,
                nation.ConnectedResource9,
                nation.ConnectedResource10,
                FileName = $"{fileName}.txt"
            }));

        // Execute stored procedure to merge data
        await connection.ExecuteAsync("update_nation_data", commandType: CommandType.StoredProcedure);
        return rowsAffected;
    }

    public async Task<int> ImportAidAsync(IReadOnlyCollection<Aid> data, string fileName)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        const string insertTempDataCommand = @"
INSERT INTO todays_file_aid
    (aid_id, sending_nation_id, sending_alliance_id, sending_team, receiving_nation_id,
    receiving_alliance_id, receiving_team, status, money, technology, soldiers, date,
    reason, file_name)
VALUES
    (@AidId, @SendingNationId, @SendingAllianceId, @SendingTeam, @ReceivingNationId,
    @ReceivingAllianceId, @ReceivingTeam, @Status, @Money, @Technology, @Soldiers,
    @Date, @Reason, @FileName)";

        // Insert into temp table
        await connection.ExecuteAsync("TRUNCATE TABLE todays_file_aid;");
        var rowsAffected = await connection.ExecuteAsync(
            insertTempDataCommand,
            data.Select(aid => new
            {
                AidId = aid.Id,
                aid.SendingNationId,
                aid.SendingAllianceId,
                SendingTeam = (int) aid.SendingTeam,
                aid.ReceivingNationId,
                aid.ReceivingAllianceId,
                ReceivingTeam = (int) aid.ReceivingTeam,
                Status = (int) aid.Status,
                aid.Money,
                aid.Technology,
                aid.Soldiers,
                aid.Date,
                aid.Reason,
                FileName = $"{fileName}.txt"
            }));

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

        const string insertTempDataCommand = @"
INSERT INTO todays_file_war
    (war_id, attacking_nation_id, attacking_alliance_id, attacking_team, 
    defending_nation_id, defending_alliance_id, defending_team, war_status, 
    begin_date, end_date, reason, destruction, attack_percent, defend_percent, 
    file_name)
VALUES
    (@WarId, @AttackingNationId, @AttackingAllianceId, @AttackingTeam,
    @DefendingNationId, @DefendingAllianceId, @DefendingTeam, @WarStatus,
    @BeginDate, @EndDate, @Reason, @Destruction, @AttackPercent, @DefendPercent,
    @FileName)";

        // Insert into temp table
        await connection.ExecuteAsync("TRUNCATE TABLE todays_file_war;");
        var rowsAffected = await connection.ExecuteAsync(
            insertTempDataCommand,
            data.Select(war => new
            {
                WarId = war.Id,
                war.AttackingNationId,
                war.AttackingAllianceId,
                AttackingTeam = (int) war.AttackingTeam,
                war.DefendingNationId,
                war.DefendingAllianceId,
                DefendingTeam = (int) war.DefendingTeam,
                WarStatus = (int) war.WarStatus,
                war.BeginDate,
                war.EndDate,
                war.Reason,
                war.Destruction,
                war.AttackPercent,
                war.DefendPercent,
                FileName = $"{fileName}.txt"
            }));

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
    }
}
