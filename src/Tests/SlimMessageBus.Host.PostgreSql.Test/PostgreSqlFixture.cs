namespace SlimMessageBus.Host.PostgreSql.Test;

using System.Diagnostics;

public class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgreSqlContainer;

    public PostgreSqlFixture()
    {
        var builder = new PostgreSqlBuilder()
            .WithImage("postgres:17.4")
            .WithReuse(true);

        if (Debugger.IsAttached)
        {
            builder = builder.WithPortBinding(50001, 5432);
        }

        _postgreSqlContainer = builder.Build();
    }

    public Task InitializeAsync() => _postgreSqlContainer.StartAsync();

    public async Task DisposeAsync()
    {
        await _postgreSqlContainer.DisposeAsync();
    }

    public string GetConnectionString() => _postgreSqlContainer.GetConnectionString();

    public async Task CreateSchema(string schema, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(GetConnectionString());
        await conn.OpenAsync(cancellationToken);

        var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            DROP SCHEMA IF EXISTS {schema} CASCADE;
            CREATE SCHEMA {schema};
            """;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
