namespace SlimMessageBus.Host.Outbox.MongoDb.Transactions;

public abstract class AbstractMongoDbTransactionService : AbstractOutboxTransactionService, IMongoDbTransactionService
{
    public abstract IClientSessionHandle? CurrentSession { get; }
}
