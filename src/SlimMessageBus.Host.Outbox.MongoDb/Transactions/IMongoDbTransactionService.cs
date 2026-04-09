namespace SlimMessageBus.Host.Outbox.MongoDb.Transactions;

public interface IMongoDbTransactionService : IAsyncDisposable
{
    IClientSessionHandle? CurrentSession { get; }
    Task BeginTransaction();
    Task CommitTransaction();
    Task RollbackTransaction();
}
