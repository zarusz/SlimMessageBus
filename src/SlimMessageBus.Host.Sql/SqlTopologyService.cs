namespace SlimMessageBus.Host.Sql;

public class SqlTopologyService : CommonSqlMigrationService<SqlRepository, SqlMessageBusSettings>
{
    public SqlTopologyService(ILogger<SqlTopologyService> logger, SqlRepository repository, ISqlTransactionService transactionService, SqlMessageBusSettings settings)
        : base(logger, repository, transactionService, settings)
    {
    }

    protected override async Task OnMigrate(CancellationToken token)
    {
    }
}
