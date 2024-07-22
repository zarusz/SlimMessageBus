namespace SlimMessageBus.Host.Outbox.Sql.Test;

public abstract class BaseSqlTest : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer;

    protected BaseSqlTest()
    {
        _sqlContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-CU13-ubuntu-22.04")
            .WithAutoRemove(true)
            .Build();
    }

    public virtual async Task DisposeAsync()
    {
        await _sqlContainer.DisposeAsync();
    }

    public virtual async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();
    }

    protected string GetConnectionString()
    {
        return _sqlContainer.GetConnectionString();
    }
}
