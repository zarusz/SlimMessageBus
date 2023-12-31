# SQL transport provider for SlimMessageBus <!-- omit in toc -->

Please read the [Introduction](intro.md) before reading this provider documentation.

- [About](#about)
- [SQL Compatibility](#sql-compatibility)
- [Configuration](#configuration)
- [How it works](#how-it-works)

## About

The SQL transport provider allows to leverage a single shared SQL database instance as a messaging broker for all the collaborating producers and consumers.

This transport might be optimal for simpler applications that do not have a dedicated messaging infrastructure available, do not have high throughput needs, or want to target a simplistic deployment model.

When the application grows over time, and given that SMB is an abstraction, the migration from SQL towards a dedicated messaging system should be super easy.

## SQL Compatibility

This transport has been tested on SQL Azure (T-SQL), and should work on most other databases.
If you see an issue, please raise an github issue.

## Configuration

ToDo: Finish

The configuration is arranged via the `.WithProviderMqtt(cfg => {})` method on the message bus builder.

```cs
services.AddSlimMessageBus(mbb =>
{
    mbb.WithProviderMqtt(cfg =>
    {
        cfg.ClientBuilder
            .WithTcpServer(configuration["Mqtt:Server"], int.Parse(configuration["Mqtt:Port"]))
            .WithTls()
            .WithCredentials(configuration["Mqtt:Username"], configuration["Mqtt:Password"])
            // Use MQTTv5 to use message headers (if the broker supports it)
            .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500);
    });

    mbb.AddServicesFromAssemblyContaining<PingConsumer>();
    mbb.AddJsonSerializer();
});
```

The `ClientBuilder` property (of type `MqttClientOptionsBuilder`) is used to configure the underlying [MQTTnet library client](https://github.com/dotnet/MQTTnet/wiki/Client).
Please consult the MQTTnet library docs for more configuration options.

## How it works

The same SQL database instance is required for all the producers and consumers to collaborate.
Therefore ensure all of the service instances point to the same database cluster.

- Single table is used to store all the exchanged messages (by default table is called `Messages`).
- Producers send messages to the messages table.
  - There are two types of entities (queues, and topics for pub/sub).
  - In the case of a topic:
    - Each subscription gets a copy of the message.
    - Subscription has a lifetime, and can expire after certain idle time. Along with it, all the messages placed on the subscription.
- Consumers (queue consumers, or subscribers in pub/sub) long poll the table to pick up their respective message.
  - Queue consumers compete for the message, and ensure only one consumer instance is processing the message.
  - Topic subscribers complete for the message within the same subscription.
- In the future we might consider:
  - Table per each entity, so that we can minimize table locking.
  - Sessions to ensure order of processing within the same message session ID - similar to how Azure Service Bus feature or Apache Kafka topic-partition works.
