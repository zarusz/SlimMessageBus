# PostgreSQL transport provider for SlimMessageBus <!-- omit in toc -->

Please read the [Introduction](intro.md) before reading this provider documentation.

- [About](#about)
- [Configuration](#configuration)
- [How it works](#how-it-works)

## About

The PostgreSQL transport provider allows a shared PostgreSQL database to act as the message broker for collaborating producers and consumers.

This transport is useful for applications that already operate PostgreSQL and do not need a dedicated messaging broker yet.

## Configuration

The configuration is arranged via the `.WithProviderPostgreSql(cfg => {})` method on the message bus builder.

```cs
services.AddSlimMessageBus(mbb =>
{
    mbb.WithProviderPostgreSql(cfg =>
    {
       cfg.ConnectionString = "...";
       cfg.DatabaseSchemaName = "smb";
       cfg.DatabaseTableName = "messages";
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

- A messages table stores exchanged messages.
- A subscriptions table stores durable topic subscriptions configured by consumers.
- Queue consumers compete for rows using `FOR UPDATE SKIP LOCKED`.
- Topic publishes create one row per configured subscription.
- Message rows use a `bigserial` physical key for insert locality and a logical `uuid` message id.
- The default client-side id generator is sequential-ish for index locality. Random database ids can be selected through `cfg.IdGeneration`.
- Producers optionally call `pg_notify` after inserting messages. Polling remains the correctness mechanism.
