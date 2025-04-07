namespace SlimMessageBus.Host.Outbox.PostgreSql.DbContext.Test.Fixtures;

using System.Text.RegularExpressions;

public class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgreSqlContainer;

    public PostgreSqlFixture()
    {
        var builder = new PostgreSqlBuilder()
            .WithImage("postgres:17.4")
            .WithDatabase($"smb_{Random.Shared.Next(10000)}")
            .WithCleanUp(true);

        if (Debugger.IsAttached)
        {
            builder = builder.WithPortBinding(50001, 5432);
        }

        _postgreSqlContainer = builder.Build();
    }

    public string GetConnectionString()
    {
        return _postgreSqlContainer.GetConnectionString();
    }

    public async Task InitializeAsync()
    {
        await _postgreSqlContainer.StartAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgreSqlContainer.DisposeAsync();
    }
}
