namespace SlimMessageBus.Host.AzureEventHub;

using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Azure.Storage.Blobs;

public class EventHubMessageBusSettings
{
    /// <summary>
    /// The Azure Event Hub connection string.
    /// </summary>
    public string ConnectionString { get; set; }

    /// <summary>
    /// The Azure Storage connection string. This will store all the group consumer offsets.
    /// </summary>
    public string StorageConnectionString { get; set; }

    /// <summary>
    /// Factory for <see cref="EventHubProducerClientOptions"/>. Called whenever a new instance needs to be created.
    /// </summary>
    public Func<string, EventHubProducerClientOptions> EventHubProducerClientOptionsFactory { get; set; }

    /// <summary>
    /// Factory for <see cref="EventHubProducerClient"/>. Called whenever a new instance needs to be created.
    /// </summary>
    public Func<string, EventHubProducerClient> EventHubProducerClientFactory { get; set; }

    /// <summary>
    /// Factory for <see cref="EventProcessorClientOptions"/>. Called whenever a new instance needs to be created.
    /// The func arguments are as follows: EventHubPath, Group.
    /// </summary>
    public Func<ConsumerParams, EventProcessorClientOptions> EventHubProcessorClientOptionsFactory { get; set; }

    /// <summary>
    /// Factory for <see cref="EventProcessorClient"/>. Called whenever a new instance needs to be created.
    /// The func arguments are as follows: EventHubPath, Group.
    /// </summary>
    public Func<ConsumerParams, EventProcessorClient> EventHubProcessorClientFactory { get; set; }

    /// <summary>
    /// Factory for <see cref="BlobContainerClient"/>. Called once for entire bus to create storage.
    /// The func arguments are as follows: EventHubPath, Group.
    /// </summary>
    public Func<BlobContainerClient> BlobContanerClientFactory { get; set; }

    /// <summary>
    /// The storage container name for leases.
    /// </summary>
    public string LeaseContainerName { get; set; }

    /// <summary>
    /// Should the checkpoint on partitions for the consumed messages happen when the bus is stopped (or disposed)?
    /// This ensures the message reprocessing is minimized in between application restarts.
    /// Default is true.
    /// </summary>
    public bool EnableCheckpointOnBusStop { get; set; } = true;

    public EventHubMessageBusSettings(string connectionString, string storageConnectionString, string leaseContainerName)
    {
        ConnectionString = connectionString;
        StorageConnectionString = storageConnectionString;
        LeaseContainerName = leaseContainerName;

        BlobContanerClientFactory = () => new BlobContainerClient(StorageConnectionString, LeaseContainerName);

        EventHubProducerClientOptionsFactory = (path) => new EventHubProducerClientOptions();
        EventHubProducerClientFactory = (path) => new EventHubProducerClient(ConnectionString, path, EventHubProducerClientOptionsFactory(path));

        EventHubProcessorClientOptionsFactory = (consumerParams) => new EventProcessorClientOptions();
        EventHubProcessorClientFactory = (consumerParams) => new EventProcessorClient(consumerParams.CheckpointClient, consumerParams.Group, ConnectionString, consumerParams.Path, EventHubProcessorClientOptionsFactory(consumerParams));
    }
}