namespace SlimMessageBus.Host.Sql;

public class SqlRepository : CommonSqlRepository, ISqlRepository
{
    public SqlRepository(ILogger<SqlRepository> logger, SqlMessageBusSettings settings, SqlConnection connection)
        : base(logger, settings, connection)
    {
    }
}
