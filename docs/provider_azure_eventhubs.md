# Azure Event Hub Provider for SlimMessageBus  <!-- omit in toc -->

Please read the [Introduction](intro.md) before reading this provider documentation.

- [Configuration](#configuration)
  - [Advanced settings](#advanced-settings)
- [Producing Messages](#producing-messages)
  - [Selecting message partition](#selecting-message-partition)
- [Consuming Messages](#consuming-messages)
  - [Checkpointing offsets](#checkpointing-offsets)

## Configuration

Azure Event Hub provider requires a connection string to the event hub:

```cs
var connectionString = ""; // Azure Event Hubs connection string
var storageConnectionString = ""; // Azure Storage Account connection string (for the consumer group to store last checkpointed offset of each topic-partition)
var storageContainerName = ""; // Azure Blob Storage container name

// MessageBusBuilder mbb;
mbb.    
    // the bus configuration here
    .WithProviderEventHub(new EventHubMessageBusSettings(connectionString, storageConnectionString, storageContainerName)); // Use Azure Event Hub as provider
    .WithSerializer(new JsonMessageSerializer());

IMessageBus bus = mbb.Build();
```

If your bus only produces messages to Event Hub and does not consume any messages, then you do not need to provide a storage account as part of the config. In that case pass `null` for the storage account details:

```cs
var connectionString = ""; // Azure Event Hubs connection string

// MessageBusBuilder mbb;
mbb.    
    // the bus configuration here
    .WithProviderEventHub(new EventHubMessageBusSettings(connectionString, null, null)); // The bus will only be used to publish messages to Azure Event Hub
    .WithSerializer(new JsonMessageSerializer());
```

> The blob storage container will be created if it does not exist. Therefore, ensure the storage account connection string has sufficient permissions or create the storage container ahead of the application start.

### Advanced settings

There are additional configuration options from the underlying AEH client available
that can be used to further tweak the client behavior. Here is an example:

```cs
var settings = new EventHubMessageBusSettings(connectionString, storageConnectionString, storageContainerName)
{
    EventHubProducerClientOptionsFactory = (path) => new Azure.Messaging.EventHubs.Producer.EventHubProducerClientOptions
    {
        Identifier = $"MyService_{Guid.NewGuid()}"
    },
    EventHubProcessorClientOptionsFactory = (consumerParams) => new Azure.Messaging.EventHubs.EventProcessorClientOptions
    {
        // Force partition lease rebalancing to happen faster (if new consumers join they can quickly gain a partition lease)
        LoadBalancingUpdateInterval = TimeSpan.FromSeconds(2),
        PartitionOwnershipExpirationInterval = TimeSpan.FromSeconds(5),
    }
};
```

## Producing Messages

To produce a given `TMessage` to an Azure Event Hub named `my-event-hub` use:

```cs
// send TMessage to Azure SB queues
mbb.Produce<TMessage>(x => x.DefaultPath("my-event-hub")); 
```

### Selecting message partition

Azure EventHub topics are broken into partitions:

- when message key is not provided then partition is assigned using round-robin
- when message key is provided then same partition is assigned to same message key

SMB Azure EventHub allows to set a provider (selector) that will assign the partition key for a given message. Here is an example:

```cs
mbb.Produce<CustomerUpdated>(x => 
    {
        x.DefaultPath("topic1");
        // Message key could be set for the message
        x.KeyProvider((message) => message.CustomerId.ToString());
    });
```

The partition key value is a `string` for AEH.

> There is also an alias `EhKeyProvider` that might be useful in case the hybrid bus is used with multiple providers that might have an overlapping extension method name.

## Consuming Messages

Azure Event Hub requires a consumer group name to be provided along with the event hub name:

```cs
mbb.Consume<SomeMessage>(x => 
    x.Path(hubName) // hub name
     .Group(consumerGroupName) // consumer group name on the hub
     .WithConsumer<SomeConsumer>())
```

### Checkpointing offsets

Azure Event Hub client needs to store the last offset for a partition / hub name / consumer group, so that when the app restarts it knows where to resume message consumption from.
That is checkpointing. Here are some additional configuration options:

```cs
mbb.Consume<SomeMessage>(x => 
    x.Path(hubName) // hub name
     .Group(consumerGroupName) // consumer group name on the hub
     .WithConsumer<SomeConsumer>()
     .CheckpointAfter(TimeSpan.FromSeconds(10)) // trigger checkpoint after 10 seconds 
     .CheckpointEvery(50)) // trigger checkpoint every 50 messages
```

When the service checkpoints are often, this will impact performance/throughput (more round trips to Azure Blob Storage to save the offsets). In contrast, when the service checkpoints are too rare, then the probability for a message retry (if the lease expires or your services crashes) increases. As with everything, this needs to be tweaked to achieve a balance.

> Since version 1.16.0 the transport has moved to the new [AEH client library](https://www.nuget.org/packages/Azure.Messaging.EventHubs/).
> Because of this, the consumer offsets stored in Azure Blob Storage are no longer compatible.
> See the migration path from Microsoft [here](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/eventhub/Azure.Messaging.EventHubs/MigrationGuide.md#migrating-eventprocessorhost-checkpoints).
