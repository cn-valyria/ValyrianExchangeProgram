using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace Valyria.AppHost;

/// <summary>
/// The local dev database: a MySQL container, migrated by Flyway, filled by the data loader's real CN import, then
/// seeded with a small VEP program (Valyria.Seeding). Run mode only; production is never touched
/// </summary>
internal static class LocalDatabase
{
    // Keep in step with FLYWAY_IMAGE in .github/workflows/ci-cd.yml
    private const string FlywayImage = "flyway/flyway";
    private const string FlywayTag = "11.20.3";

    // cn-valyria-prod's sql_mode (az mysql flexible-server parameter show --name sql_mode): the default minus STRICT_TRANS_TABLES
    private const string ProductionSqlMode = "ERROR_FOR_DIVISION_BY_ZERO,NO_ZERO_DATE,NO_ZERO_IN_DATE,ONLY_FULL_GROUP_BY,NO_ENGINE_SUBSTITUTION";

    public static void AddLocalDatabase(this IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> api, IResourceBuilder<ProjectResource> dataLoader)
    {
        var databaseDirectory = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "../Valyria.Database"));

        // Fixed rather than generated, since the data volume keeps the password it was first created with
        var password = builder.AddParameter("LocalMySqlPassword", "valyria-local", secret: true);

        // The init script creates the base_admin user the views and procedures are defined with, on first start.
        // Production's sql_mode isn't strict, so over-long CN values (e.g. aid reasons over 25 characters) are
        // truncated instead of failing the import. Match it, or the real files can't be imported
        var mysql = builder.AddMySql("mysql", password)
            .WithImageTag("8.0")
            .WithArgs($"--sql-mode={ProductionSqlMode}")
            .WithDataVolume("valyria-local-mysql")
            .WithInitFiles(Path.Combine(databaseDirectory, "local", "mysql-init"));
        var vepDb = mysql.AddDatabase("vep-db", databaseName: "vep_db");
        var cnDb = mysql.AddDatabase("cybernations-db", databaseName: "cybernations_db");

        // Same image, settings and flags as CI. vep_db is the default schema (V8 has no USE statement)
        var endpoint = mysql.Resource.PrimaryEndpoint;
        var migrations = builder.AddContainer("db-migrations", FlywayImage, FlywayTag)
            .WithBindMount(databaseDirectory, "/flyway/project", isReadOnly: true)
            .WithEnvironment("FLYWAY_URL", ReferenceExpression.Create(
                $"jdbc:mysql://{endpoint.Property(EndpointProperty.Host)}:{endpoint.Property(EndpointProperty.Port)}/vep_db?allowPublicKeyRetrieval=true&useSSL=false"))
            .WithEnvironment("FLYWAY_USER", "root")
            .WithEnvironment("FLYWAY_PASSWORD", password)
            .WithArgs("-workingDirectory=/flyway/project", "migrate")
            .WaitFor(vepDb)
            .WaitFor(cnDb);

        var vepDbConnectionString = ConnectionString(vepDb);
        var cnDbConnectionString = ConnectionString(cnDb);

        api.WithEnvironment("VepDbConnectionString", vepDbConnectionString)
            .WaitForCompletion(migrations);

        // CnFileSource=Local serves the saved files in Valyria.Database/local/cn-files instead of downloading today's
        dataLoader
            .WithEnvironment("VepDbConnectionString", vepDbConnectionString)
            .WithEnvironment("CybernationsDbConnectionString", cnDbConnectionString)
            .WithEnvironment("CnFileSource", builder.Configuration["CnFileSource"] ?? "Remote")
            .WithEnvironment("CnSavedFilesPath", Path.Combine(databaseDirectory, "local", "cn-files"))
            .WaitForCompletion(migrations);

        var seeder = builder.AddProject<Projects.Valyria_Seeding>("db-seed")
            .WithEnvironment("VepDbConnectionString", vepDbConnectionString)
            .WaitForCompletion(dataLoader);

        // Seeded accounts have no Discord ID, so nobody is pinged. Set Seed:DevDiscordId in the AppHost's user
        // secrets to put yours on the seeded Super Admin, to test the pings and the Discord bot
        if (builder.Configuration["Seed:DevDiscordId"] is { Length: > 0 } devDiscordId)
            seeder.WithEnvironment("Seed__DevDiscordId", devDiscordId);

        seeder.WithCommand("reset-and-reseed", "Reset & reseed",
            context => ResetAndReseedAsync(context, vepDb.Resource, dataLoader.Resource, seeder.Resource),
            new CommandOptions
            {
                Description = "Delete all VEP data, import any newer CN files and seed again, with dates from today",
                ConfirmationMessage = "Delete every account, deal and flag in the local vep_db and seed it again?",
                IconName = "ArrowCounterclockwise",
            });
    }

    // MySql.Data (used by the repositories) needs this to log in over a connection without TLS
    private static ReferenceExpression ConnectionString(IResourceBuilder<MySqlDatabaseResource> database) =>
        ReferenceExpression.Create($"{database.Resource.ConnectionStringExpression};AllowPublicKeyRetrieval=true");

    private static async Task<ExecuteCommandResult> ResetAndReseedAsync(ExecuteCommandContext context,
        MySqlDatabaseResource vepDb, IResource dataLoader, IResource seeder)
    {
        var cancellationToken = context.CancellationToken;
        var commands = context.Services.GetRequiredService<ResourceCommandService>();
        var notifications = context.Services.GetRequiredService<ResourceNotificationService>();

        // Everything in vep_db but the mass message templates (seeded by V11) and Flyway's history. The CN data
        // stays; the data loader brings it up to date below
        var connectionString = await vepDb.ConnectionStringExpression.GetValueAsync(cancellationToken);
        await using (var connection = new MySqlConnection(connectionString))
        {
            await connection.OpenAsync(cancellationToken);

            var tables = new List<string>();
            await using (var query = new MySqlCommand(@"
                select table_name
                from information_schema.tables
                where table_schema = 'vep_db'
                    and table_type = 'BASE TABLE'
                    and table_name not in ('mass_message', 'flyway_schema_history')", connection))
            await using (var reader = await query.ExecuteReaderAsync(cancellationToken))
                while (await reader.ReadAsync(cancellationToken))
                    tables.Add(reader.GetString(0));

            var resetSql = "SET FOREIGN_KEY_CHECKS = 0;\n" +
                           string.Concat(tables.Select(table => $"DELETE FROM `vep_db`.`{table}`;\n")) +
                           "SET FOREIGN_KEY_CHECKS = 1;";
            await using var reset = new MySqlCommand(resetSql, connection);
            await reset.ExecuteNonQueryAsync(cancellationToken);
        }

        context.Logger.LogInformation("Local vep_db reset. Running the data loader, then the seed");

        // The seed is built on the latest aid, so import any newer CN files first
        var loaderResult = await RunToCompletionAsync(commands, notifications, dataLoader, cancellationToken);
        if (loaderResult is not null)
            return loaderResult;

        return await RunToCompletionAsync(commands, notifications, seeder, cancellationToken)
               ?? CommandResults.Success();
    }

    /// <summary>
    /// Starts a resource that runs once and waits for it to exit. Returns a failure if it doesn't exit cleanly
    /// </summary>
    private static async Task<ExecuteCommandResult?> RunToCompletionAsync(ResourceCommandService commands,
        ResourceNotificationService notifications, IResource resource, CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;
        var start = await commands.ExecuteCommandAsync(resource, KnownResourceCommands.StartCommand, cancellationToken);
        if (!start.Success)
            return CommandResults.Failure($"Couldn't start {resource.Name}: {start.Message}");

        var stopped = await notifications.WaitForResourceAsync(resource.Name,
            resourceEvent => resourceEvent.Snapshot.StopTimeStamp > startedAt
                             && KnownResourceStates.TerminalStates.Contains(resourceEvent.Snapshot.State?.Text),
            cancellationToken);

        return stopped.Snapshot.ExitCode is 0 or null
            ? null
            : CommandResults.Failure($"{resource.Name} exited with code {stopped.Snapshot.ExitCode}. See its logs");
    }
}
