namespace SlimMessageBus.Host.Outbox.MongoDb.Test;

using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using SlimMessageBus.Host.Interceptor;
using SlimMessageBus.Host.Outbox.MongoDb.Interceptors;

/// <summary>
/// Exercises the DI registration lambdas inside <see cref="Configuration.MessageBusBuilderExtensions.AddOutboxUsingMongoDb"/>
/// by executing the relevant <see cref="MessageBusBuilder.PostConfigurationActions"/> entry and
/// resolving each registered service from the built container.
/// </summary>
public class MessageBusBuilderExtensionsRegistrationTests
{
    private static ServiceProvider BuildProvider(MessageBusBuilder mbb, int actionIndex)
    {
        var mongoClientMock = new Mock<IMongoClient>();
        var mongoDatabaseMock = new Mock<IMongoDatabase>();
        var outboxCollMock = new Mock<IMongoCollection<MongoDbOutboxDocument>>();
        var lockCollMock = new Mock<IMongoCollection<MongoDbOutboxLockDocument>>();

        mongoClientMock
            .Setup(x => x.GetDatabase(It.IsAny<string>(), It.IsAny<MongoDatabaseSettings>()))
            .Returns(mongoDatabaseMock.Object);

        mongoDatabaseMock
            .Setup(x => x.GetCollection<MongoDbOutboxDocument>(It.IsAny<string>(), It.IsAny<MongoCollectionSettings>()))
            .Returns(outboxCollMock.Object);

        mongoDatabaseMock
            .Setup(x => x.GetCollection<MongoDbOutboxLockDocument>(It.IsAny<string>(), It.IsAny<MongoCollectionSettings>()))
            .Returns(lockCollMock.Object);

        var services = new ServiceCollection();
        services.AddSingleton<IMongoClient>(mongoClientMock.Object);
        services.AddSingleton(TimeProvider.System);
        services.AddLogging();

        // Execute only the AddOutboxUsingMongoDb PostConfigurationAction (not AddOutbox ones)
        mbb.PostConfigurationActions[actionIndex](services);

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task When_AddOutboxUsingMongoDb_Given_NoConfiguration_Then_DefaultServicesAreResolvable()
    {
        // arrange
        var mbb = MessageBusBuilder.Create();
        int idx = mbb.PostConfigurationActions.Count;
        mbb.AddOutboxUsingMongoDb();

        await using var sp = BuildProvider(mbb, idx);
        await using var scope = sp.CreateAsyncScope();
        var scoped = scope.ServiceProvider;

        // assert — singleton services
        sp.GetRequiredService<MongoDbOutboxSettings>().Should().NotBeNull();
        sp.GetRequiredService<MongoDbSettings>().Should().NotBeNull();
        sp.GetRequiredService<IMongoDatabase>().Should().NotBeNull();
        sp.GetRequiredService<IMongoCollection<MongoDbOutboxDocument>>().Should().NotBeNull();
        sp.GetRequiredService<IMongoCollection<MongoDbOutboxLockDocument>>().Should().NotBeNull();

        // assert — scoped services
        scoped.GetRequiredService<MongoDbSessionHolder>().Should().NotBeNull();
        scoped.GetRequiredService<IMongoDbTransactionService>().Should().NotBeNull();
        scoped.GetRequiredService<IMongoDbMessageOutboxRepository>().Should().NotBeNull();
        scoped.GetRequiredService<IOutboxMessageRepository<MongoDbOutboxMessage>>().Should().NotBeNull();

        // assert — transient service
        sp.GetRequiredService<IOutboxMigrationService>().Should().BeOfType<MongoDbOutboxMigrationService>();
    }

    [Fact]
    public async Task When_AddOutboxUsingMongoDb_Given_ConfigureCallback_Then_CustomSettingsApplied()
    {
        // arrange
        var mbb = MessageBusBuilder.Create();
        int idx = mbb.PostConfigurationActions.Count;
        mbb.AddOutboxUsingMongoDb(s =>
        {
            s.MongoDbSettings.DatabaseName = "custom-db";
            s.MongoDbSettings.CollectionName = "custom-outbox";
        });

        await using var sp = BuildProvider(mbb, idx);

        // assert
        var settings = sp.GetRequiredService<MongoDbSettings>();
        settings.DatabaseName.Should().Be("custom-db");
        settings.CollectionName.Should().Be("custom-outbox");
    }

    [Fact]
    public async Task When_AddOutboxUsingMongoDb_Given_ConsumerWithMongoDbTransactionEnabled_Then_InterceptorRegistered()
    {
        // arrange
        var mbb = MessageBusBuilder.Create();
        mbb.Consume<SampleMessage>(x => x
            .Path("test-topic")
            .WithConsumer<SampleConsumer>()
            .UseMongoDbTransaction(enabled: true));

        int idx = mbb.PostConfigurationActions.Count;
        mbb.AddOutboxUsingMongoDb();

        await using var sp = BuildProvider(mbb, idx);

        // assert — the MongoDbTransaction interceptor for SampleMessage should be registered
        var interceptors = sp.GetServices<IConsumerInterceptor<SampleMessage>>().ToList();
        interceptors.Should().ContainSingle(i => i is MongoDbTransactionConsumerInterceptor<SampleMessage>);
    }

    // ── sample types ─────────────────────────────────────────────────────────
    public record SampleMessage;

    public class SampleConsumer : IConsumer<SampleMessage>
    {
        public Task OnHandle(SampleMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
