namespace SlimMessageBus.Host.Outbox.PostgreSql.DbContext.Test.Fixtures;

public class KafkaFixture : IAsyncLifetime
{
    private readonly KafkaContainer _kafkaContainer;

    public KafkaFixture()
    {
        var builder = new KafkaBuilder();

        _kafkaContainer = builder.Build();
    }

    public string GetBootstrapAddress()
    {
        return _kafkaContainer.GetBootstrapAddress();
    }

    public async Task CreateTopics(params string[] topics)
    {
        var adminConfig = new AdminClientConfig
        {
            BootstrapServers = GetBootstrapAddress()
        };

        using var adminClient = new AdminClientBuilder(adminConfig).Build();
        await adminClient.CreateTopicsAsync(topics.Select(topic => new TopicSpecification { Name = topic, NumPartitions = 1 }));
    }

    public async Task InitializeAsync()
    {
        await _kafkaContainer.StartAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _kafkaContainer.DisposeAsync();
    }
}