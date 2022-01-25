# Azure Event Hub Provider for SlimMessageBus  <!-- omit in toc -->

Please read the [Introduction](intro.md) before reading this provider documentation.

- [Configuration](#configuration)
- [Producing Messages](#producing-messages)
  - [Selecting message partition for producer](#selecting-message-partition-for-producer)

## Configuration

Azure Event Hub provider requires a connection string to the event hub:

```cs
var connectionString = ""; // Azure Event Hubs connection string
var storageConnectionString = ""; // Azure Storage Account connection string (for the consumer)
var storageContainerName = ""; // Azure Blob Storage container name (for the consumer to store last commit offset of each subscriber)

MessageBusBuilder mbb = MessageBusBuilder.Create()
    // the bus configuration here
    .WithProviderEventHub(new EventHubMessageBusSettings(connectionString, storageConnectionString, storageContainerName)); // Use Azure Event Hub as provider
    .WithSerializer(new JsonMessageSerializer());

IMessageBus bus = mbb.Build();
```

If your bus does only produce messages to Event Hub and does not consume any messages, then you do not need to provide a storage account as part of the config. In this case pass `null` for the storage account details:

```cs
var connectionString = ""; // Azure Event Hubs connection string

MessageBusBuilder mbb = MessageBusBuilder.Create()
    // the bus configuration here
    .WithProviderEventHub(new EventHubMessageBusSettings(connectionString, null, null)); // The bus will only be used to publish messages to Azure Event Hub
    .WithSerializer(new JsonMessageSerializer());
```

## Producing Messages

To produce a given `TMessage` to an Azure Event Hub named `my-event-hub` use:

```cs
// send TMessage to Azure SB queues
mbb.Produce<TMessage>(x => x.DefaultTopic("my-event-hub")); 
```

### Selecting message partition for producer

Azure EventHub topics are broken into partitions:

- when message key is not provided then partition is assigned using round-robi
- when message key is provided then same partition is assigned to same key

SMB Azure EventHub allows to set a provider (selector) that will assign the partition key for a given message. Here is an example:

```cs
mbb.Produce<CustomerUpdated>(x => 
    {
        x.DefaultTopic("topic1");
        // Message key could be set for the message
        x.KeyProvider((message) => CustomerId.ToString());
    });
```

The partition key value is a `string` for AEH.

> There is also an alias `EhKeyProvider` that might be useful in case the hybrid bus is used with multiple providers that might have an overlapping extension method name.