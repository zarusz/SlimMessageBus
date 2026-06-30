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

This transport targets SQL Server / Azure SQL (T-SQL).

## Configuration

The configuration is arranged via the `.WithProviderSql(cfg => {})` method on the message bus builder.

```cs
services.AddSlimMessageBus(mbb =>
{
    mbb.WithProviderSql(cfg =>
    {
       cfg.ConnectionString = "...";
       cfg.DatabaseSchemaName = "smb";
       cfg.DatabaseTableName = "Messages";
       cfg.PollDelay = TimeSpan.FromMilliseconds(250);
       cfg.PollBatchSize = 10;
    });

    mbb.Produce<PingMessage>(x => x.DefaultQueue("ping-queue"));
    mbb.Consume<PingMessage>(x => x.Queue("ping-queue"));

    mbb.Produce<OrderSubmitted>(x => x.DefaultTopic("orders").ToTopic());
    mbb.Consume<OrderSubmitted>(x => x.Topic("orders", "billing"));
    mbb.Consume<OrderSubmitted>(x => x.Topic("orders", "shipping"));

    mbb.AddServicesFromAssemblyContaining<PingConsumer>();
    mbb.AddJsonSerializer();
});
```

## How it works

The same SQL database instance is required for all the producers and consumers to collaborate.
Therefore ensure all of the service instances point to the same database cluster.

- A messages table is used to store exchanged messages (by default table is called `Messages`).
- A subscriptions table is used to store durable topic subscriptions configured by consumers.
- Producers send messages to the messages table.
  - There are two types of entities (queues, and topics for pub/sub).
  - In the case of a topic:
    - Each configured durable subscription gets a copy of the message.
- Consumers (queue consumers, or subscribers in pub/sub) long poll the table to pick up their respective message.
  - Queue consumers compete for the message, and ensure only one consumer instance is processing the message.
  - Topic subscribers compete for the message within the same subscription.
- Message rows use a clustered `bigint identity` sequence for insert locality and a logical `uniqueidentifier` message id.
- The default client-side id generator is sequential-ish for SQL Server index locality. Random database ids and database-generated sequential ids can be selected through `cfg.IdGeneration`.
- In the future we might consider:
  - Table per each entity, so that we can minimize table locking.
  - Sessions to ensure order of processing within the same message session ID - similar to how Azure Service Bus feature or Apache Kafka topic-partition works.
