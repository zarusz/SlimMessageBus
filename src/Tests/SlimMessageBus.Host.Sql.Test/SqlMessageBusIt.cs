namespace SlimMessageBus.Host.Sql.Test;

[Trait("Category", "Integration")]
[Trait("Transport", "Sql")]
[Collection(nameof(SqlServerCollection))]
public class SqlMessageBusIt(ITestOutputHelper output, SqlServerFixture sqlServerFixture) : BaseIntegrationTest<SqlMessageBusIt>(output)
{
    private readonly string _schemaName = $"smb_{Guid.NewGuid():N}";

    protected override void SetupServices(ServiceCollection services, IConfigurationRoot configuration)
    {
        services.AddSlimMessageBus(mbb =>
        {
            mbb.WithProviderSql(cfg =>
            {
                cfg.ConnectionString = sqlServerFixture.GetConnectionString();
                cfg.DatabaseSchemaName = _schemaName;
                cfg.DatabaseTableName = "Messages";
                cfg.PollDelay = TimeSpan.FromMilliseconds(100);
                cfg.PollBatchSize = 5;
            });

            ApplyBusConfiguration(mbb);

            mbb.AddServicesFromAssemblyContaining<PingConsumer>();
            mbb.AddJsonSerializer();
        });

        services.AddSingleton<TestEventCollector<PingMessage>>();
    }

    public override async Task InitializeAsync()
    {
        await sqlServerFixture.CreateSchema(_schemaName);
    }

    [Fact]
    public async Task BasicQueue()
    {
        var queue = $"queue-{Guid.NewGuid():N}";

        AddBusConfiguration(mbb =>
        {
            mbb.Produce<PingMessage>(x => x.DefaultQueue(queue));
            mbb.Consume<PingMessage>(x => x.Queue(queue));
        });

        var messageBus = ServiceProvider.GetRequiredService<IMessageBus>();
        var consumedMessages = ServiceProvider.GetRequiredService<TestEventCollector<PingMessage>>();

        await messageBus.Publish(new PingMessage(1, "one"));
        await consumedMessages.WaitUntilArriving(expectedCount: 1);

        consumedMessages.Snapshot().Should().ContainSingle(x => x.Id == 1 && x.Value == "one");
    }

    [Fact]
    public async Task BasicTopicWithDurableSubscriptions()
    {
        var topic = $"topic-{Guid.NewGuid():N}";

        AddBusConfiguration(mbb =>
        {
            mbb.Produce<PingMessage>(x => x.DefaultTopic(topic).ToTopic());
            mbb.Consume<PingMessage>(x => x.Topic(topic, "subscriber-a"));
            mbb.Consume<PingMessage>(x => x.Topic(topic, "subscriber-b"));
        });

        var messageBus = ServiceProvider.GetRequiredService<IMessageBus>();
        var consumedMessages = ServiceProvider.GetRequiredService<TestEventCollector<PingMessage>>();

        await EnsureConsumersStarted();
        await messageBus.Publish(new PingMessage(2, "two"));
        await consumedMessages.WaitUntilArriving(expectedCount: 2);

        consumedMessages.Snapshot().Should().HaveCount(2);
        consumedMessages.Snapshot().Should().AllSatisfy(x =>
        {
            x.Id.Should().Be(2);
            x.Value.Should().Be("two");
        });
    }

    public record PingMessage(int Id, string Value);

    public class PingConsumer(TestEventCollector<PingMessage> messages) : IConsumer<PingMessage>
    {
        public Task OnHandle(PingMessage message, CancellationToken cancellationToken)
        {
            messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
