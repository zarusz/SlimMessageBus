namespace SlimMessageBus.Host.Sql;

public class SqlTopologyService : CommonSqlMigrationService<SqlRepository, SqlMessageBusSettings>
{
    public SqlTopologyService(ILogger<SqlTopologyService> logger, SqlRepository repository, SqlMessageBusSettings settings)
        : base(logger, repository, settings)
    {
    }

    protected override async Task OnMigrate(CancellationToken token)
    {
    }
}
