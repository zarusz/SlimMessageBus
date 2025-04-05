namespace SlimMessageBus.Host.Outbox.Sql.Test;

public class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer;

    public SqlServerFixture()
    {
        var builder = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-CU13-ubuntu-22.04")
            .WithReuse(true);

        if (Debugger.IsAttached)
        {
            builder = builder.WithPortBinding(50000, 1433);
        }

        _sqlContainer = builder.Build();
    }

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _sqlContainer.DisposeAsync();
    }

    public string GetConnectionString()
    {
        return _sqlContainer.GetConnectionString();
    }

    public async Task CreateSchema(string schema, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqlConnection(GetConnectionString());
        await conn.OpenAsync(cancellationToken);

        var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            EXEC sp_MSforeachtable @command1 = 'DROP TABLE ?', @whereand = 'AND SCHEMA_NAME(schema_id) = "{schema}";'
            DROP SCHEMA IF EXISTS "{schema}";
            """;
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE SCHEMA \"{schema}\";";
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        await conn.CloseAsync();
    }
}
