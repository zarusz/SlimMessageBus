namespace SlimMessageBus.Host.Relational.Benchmark;

using BenchmarkDotNet.Attributes;

using Npgsql;

using SlimMessageBus.Host.PostgreSql;

using Testcontainers.PostgreSql;

[MemoryDiagnoser]
public class PostgreSqlQueueTransportBenchmark : AbstractRelationalTransportBenchmark
{
    private PostgreSqlContainer _container;

    protected override async Task StartInfrastructure()
    {
        _container = new PostgreSqlBuilder("postgres:17.4")
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
        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""CREATE SCHEMA "{schemaName}";""";
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    protected override async Task DropSchema(string schemaName)
    {
        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""DROP SCHEMA IF EXISTS "{schemaName}" CASCADE;""";
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    protected override void ConfigureProvider(MessageBusBuilder mbb, string schemaName)
        => mbb.WithProviderPostgreSql(settings =>
        {
            settings.ConnectionString = _container.GetConnectionString();
            settings.DatabaseSchemaName = schemaName;
            settings.DatabaseTableName = "messages";
            settings.PollBatchSize = 32;
            settings.PollDelay = TimeSpan.FromMilliseconds(5);
        });
}
