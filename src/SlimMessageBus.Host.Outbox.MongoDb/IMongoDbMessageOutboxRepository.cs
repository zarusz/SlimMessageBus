namespace SlimMessageBus.Host.Outbox.MongoDb;

public interface IMongoDbMessageOutboxRepository : IOutboxMessageRepository<MongoDbOutboxMessage>, IOutboxMessageFactory
{
}
