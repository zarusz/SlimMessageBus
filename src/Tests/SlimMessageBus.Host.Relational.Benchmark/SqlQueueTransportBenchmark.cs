namespace SlimMessageBus.Host.Relational.Benchmark;

using BenchmarkDotNet.Attributes;

using Microsoft.Data.SqlClient;

using SlimMessageBus.Host.Sql;

using Testcontainers.MsSql;

[MemoryDiagnoser]
public class SqlQueueTransportBenchmark : AbstractRelationalTransportBenchmark
{
    private MsSqlContainer _container;

    protected override async Task StartInfrastructure()
    {
        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-CU13-ubuntu-22.04")
            .WithReuse(true)
            .Build();

        await _container.StartAsync().ConfigureAwait(false);
    }

    protected override async Task StopInfrastructure()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync().ConfigureAwait(false);
            _container = null;
        }
    }

    protected override async Task CreateSchema(string schemaName)
    {
        await using var connection = new SqlConnection(_container.GetConnectionString());
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE SCHEMA [{schemaName}]";
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    protected override async Task DropSchema(string schemaName)
    {
        await using var connection = new SqlConnection(_container.GetConnectionString());
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            DECLARE @schemaName sysname = N'{schemaName}';
            DECLARE @sql nvarchar(max) = N'';

            SELECT @sql += N'DROP TABLE ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N';'
            FROM sys.tables
            WHERE schema_id = SCHEMA_ID(@schemaName);

            EXEC sp_executesql @sql;
            EXEC(N'DROP SCHEMA ' + QUOTENAME(@schemaName));
            """;
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    protected override void ConfigureProvider(MessageBusBuilder mbb, string schemaName)
        => mbb.WithProviderSql(settings =>
        {
            settings.ConnectionString = _container.GetConnectionString();
            settings.DatabaseSchemaName = schemaName;
            settings.DatabaseTableName = "Messages";
            settings.PollBatchSize = 32;
            settings.PollDelay = TimeSpan.FromMilliseconds(5);
        });
}
