using Dapper;
using MySql.Data.MySqlClient;
using Testcontainers.MySql;
using Valyria.Seeding;

namespace Valyria.IntegrationTests;

/// <summary>
/// One MySQL 8 container for the whole test run, built from the real Flyway migrations.
/// Tests share it serially (see <see cref="MySqlCollection"/>) and wipe the data before each test.
/// </summary>
public sealed class MySqlFixture : IAsyncLifetime
{
    private const string RootPassword = "valyria-tests";

    private readonly MySqlContainer _container = new MySqlBuilder("mysql:8.0")
        .WithUsername("root")
        .WithPassword(RootPassword)
        .WithDatabase("vep_db")
        .WithResourceMapping(new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "migrations")), "/migrations/")
        .Build();

    private string _resetSql = string.Empty;

    public string VepConnectionString => ConnectionString("vep_db");

    public string CnConnectionString => ConnectionString("cybernations_db");

    private string ConnectionString(string database) =>
        $"Server={_container.Hostname};Port={_container.GetMappedPublicPort(3306)};Database={database};" +
        $"Uid=root;Pwd={RootPassword};AllowPublicKeyRetrieval=true";

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // The views are created with DEFINER=`base_admin`, which has to exist for them to be queried
        await ExecInContainer("mysql -uroot -p\"$MYSQL_ROOT_PASSWORD\" -e " +
            "\"CREATE USER IF NOT EXISTS 'base_admin'@'%' IDENTIFIED BY 'unused'; GRANT ALL ON *.* TO 'base_admin'@'%';\"");

        // vep_db is the default schema, matching Flyway's JDBC URL (V8 has no USE statement)
        var migrations = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "migrations"), "V*__*.sql")
            .Select(Path.GetFileName)
            .OrderBy(name => int.Parse(name![1..name.IndexOf("__", StringComparison.Ordinal)]));
        foreach (var migration in migrations)
            await ExecInContainer($"mysql -uroot -p\"$MYSQL_ROOT_PASSWORD\" vep_db < /migrations/{migration}");

        // Everything but the mass messages seeded by V11 is wiped between tests. The CN tables' delete
        // triggers write audit rows keyed on (id, second), which can collide with a test's own update
        // audits, so the audit tables are cleared both before and after the rest
        using var connection = new MySqlConnection(VepConnectionString);
        var tables = (await connection.QueryAsync<string>(@"
            select concat('`', table_schema, '`.`', table_name, '`')
            from information_schema.tables
            where table_schema in ('vep_db', 'cybernations_db')
                and table_type = 'BASE TABLE'
                and table_name <> 'mass_message'
            order by table_schema, table_name")).ToList();
        var auditTables = tables.Where(table => table.Contains("`audit_")).ToList();
        _resetSql = "SET FOREIGN_KEY_CHECKS = 0;\n" +
                    string.Concat(auditTables.Concat(tables).Concat(auditTables).Select(table => $"DELETE FROM {table};\n")) +
                    "SET FOREIGN_KEY_CHECKS = 1;";
    }

    public async Task ResetAsync()
    {
        using var connection = new MySqlConnection(VepConnectionString);
        await connection.ExecuteAsync(_resetSql);
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    private async Task ExecInContainer(string command)
    {
        var result = await _container.ExecAsync(["sh", "-c", command]);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"'{command}' failed with exit code {result.ExitCode}: {result.Stderr}");
    }
}

[CollectionDefinition(nameof(MySqlCollection))]
public class MySqlCollection : ICollectionFixture<MySqlFixture>;

/// <summary>
/// Base class for repository tests: starts every test with empty tables
/// </summary>
[Collection(nameof(MySqlCollection))]
public abstract class RepositoryTestBase(MySqlFixture fixture) : IAsyncLifetime
{
    protected MySqlFixture Fixture { get; } = fixture;

    protected SeedData Data { get; } = new(fixture.VepConnectionString);

    public Task InitializeAsync() => Fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;
}
