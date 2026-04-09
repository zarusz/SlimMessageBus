namespace SlimMessageBus.Host.Outbox.PostgreSql.Transactions;

public abstract class AbstractPostgreSqlTransactionService : AbstractOutboxTransactionService, IPostgreSqlTransactionService
{
    protected AbstractPostgreSqlTransactionService(NpgsqlConnection connection)
    {
        Connection = connection;
    }

    public NpgsqlConnection Connection { get; }

    public abstract NpgsqlTransaction? CurrentTransaction { get; }
}
