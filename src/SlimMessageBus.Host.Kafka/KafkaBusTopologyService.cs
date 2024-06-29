namespace SlimMessageBus.Host.Kafka;

internal class KafkaBusTopologyService : BusTopologyService<KafkaMessageBusSettings>
{
    public KafkaBusTopologyService(
        ILogger<KafkaBusTopologyService> logger,
        MessageBusSettings settings,
        KafkaMessageBusSettings providerSettings)
        : base(logger, settings, providerSettings)
    {
    }

    protected async override Task OnProvisionTopology()
    {
        var adminClientConfig = new AdminClientConfig
        {
            BootstrapServers = ProviderSettings.BrokerList
        };
        ProviderSettings.AdminClientConfig(adminClientConfig);

        using var adminClient = ProviderSettings.AdminClientBuilderFactory(adminClientConfig).Build();

        var topics = Settings.Consumers.Select(x => x.Path)
            .Union(Settings.Producers.Select(x => x.DefaultPath))
            .Distinct()
            .ToList();

        Logger.LogInformation("Provisioning topics: {Topics}", string.Join(",", topics));

        var topicSpecs = topics
            .Select(x => new TopicSpecification
            {
                Name = x,
                // ToDo: make configurable
                //NumPartitions = ProviderSettings.DefaultNumPartitions,
                //ReplicationFactor = ProviderSettings.DefaultReplicationFactor,
            })
            .ToList();

        try
        {
            await adminClient.CreateTopicsAsync(topicSpecs);
        }
        catch (CreateTopicsException e)
        {
            Logger.LogError(e, "An error occurred creating topics");
            foreach (var result in e.Results)
            {
                Logger.LogError(e, "Topic {Topic} creation result: {Result}", result.Topic, result.Error.Reason);
            }
        }
    }
}