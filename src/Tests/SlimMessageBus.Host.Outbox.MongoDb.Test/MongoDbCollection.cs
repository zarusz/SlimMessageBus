namespace SlimMessageBus.Host.Outbox.MongoDb.Test;

[CollectionDefinition(nameof(MongoDbCollection))]
public class MongoDbCollection : ICollectionFixture<MongoDbFixture>
{
}
