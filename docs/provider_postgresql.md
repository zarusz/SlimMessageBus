# PostgreSQL transport provider for SlimMessageBus <!-- omit in toc -->

Please read the [Introduction](intro.md) before reading this provider documentation.

- [About](#about)
- [Configuration](#configuration)
  - [Provider settings](#provider-settings)
  - [Queues, topics, and request/response](#queues-topics-and-requestresponse)
  - [Message id generation](#message-id-generation)
- [How it works](#how-it-works)
  - [Polling and locking](#polling-and-locking)
  - [Retries and failed messages](#retries-and-failed-messages)
  - [Schema provisioning](#schema-provisioning)
- [Testing locally](#testing-locally)

## About

The PostgreSQL transport provider allows a shared PostgreSQL database to act as the message broker for collaborating producers and consumers.

This transport is useful for applications that already operate PostgreSQL and do not need a dedicated messaging broker yet.

## Configuration

Install the transport package:

```bash
dotnet add package SlimMessageBus.Host.PostgreSql
```

The configuration is arranged via the `.WithProviderPostgreSql(cfg => {})` method on the message bus builder.

```cs
using SlimMessageBus.Host.PostgreSql;

services.AddSlimMessageBus(mbb =>
{
    mbb.WithProviderPostgreSql(cfg =>
    {
       cfg.ConnectionString = "...";
       cfg.DatabaseSchemaName = "smb";
       cfg.DatabaseTableName = "messages";
       cfg.PollDelay = TimeSpan.FromMilliseconds(250);
       cfg.PollBatchSize = 10;
       cfg.LockDuration = TimeSpan.FromSeconds(30);
       cfg.MaxDeliveryAttempts = 10;
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

### Provider settings

The most commonly configured settings are:

- `ConnectionString` - required PostgreSQL connection string.
- `DatabaseSchemaName` - schema containing the transport tables. Defaults to `public`.
- `DatabaseTableName` - base message table name. The durable subscription table uses the same base name with a `_subscriptions` suffix.
- `DatabaseMigrationsTableName` - table used to track transport schema migrations.
- `CommandTimeout` - optional PostgreSQL command timeout.
- `TransactionIsolationLevel` - isolation level used for schema provisioning. Defaults to `ReadCommitted`.
- `PollDelay` - delay used when no message is available or after a transient polling error.
- `PollBatchSize` - maximum number of messages locked by one polling operation.
- `LockDuration` - how long a message lock is held before another consumer may pick it up.
- `MaxDeliveryAttempts` - number of processing attempts before a message is marked aborted.
- `NotifyOnPublish` - when enabled, producers call `pg_notify` after inserting messages. Polling remains the correctness mechanism.
- `SchemaCreationRetry` and `OperationRetry` - retry settings for schema creation and regular database operations.

PostgreSQL identifiers configured through `DatabaseSchemaName`, `DatabaseTableName`, and `DatabaseMigrationsTableName` are validated and quoted. Use letters, numbers, and underscores, and do not start identifiers with a number.

### Queues, topics, and request/response

Use `DefaultQueue()` and `Queue()` for competing-consumer queues:

```cs
mbb.Produce<PingMessage>(x => x.DefaultQueue("ping-queue"));
mbb.Consume<PingMessage>(x => x.Queue("ping-queue"));
```

Use `DefaultTopic().ToTopic()` and `Topic(topic, subscriptionName)` for durable pub/sub:

```cs
mbb.Produce<OrderSubmitted>(x => x.DefaultTopic("orders").ToTopic());
mbb.Consume<OrderSubmitted>(x => x.Topic("orders", "billing"));
mbb.Consume<OrderSubmitted>(x => x.Topic("orders", "shipping"));
```

Request/response endpoints can also use PostgreSQL queues or topics:

```cs
mbb.Handle<PingRequest, PingResponse>(x => x.Queue("ping-handler"));
mbb.ExpectRequestResponses(x => x.ReplyToQueue("replies"));
```

### Message id generation

The transport stores messages with two identifiers:

- `sequence_id` - a `bigserial` physical key used for insert locality and ordered polling.
- `id` - a logical `uuid` message id used by the transport when completing or failing messages.

By default, PostgreSQL uses `PostgreSqlMessageIdGenerationMode.ClientGuidGenerator` with `PostgreSqlSequentialGuidGenerator`, which creates sequential-ish UUIDs client-side for better index locality than random UUIDs.

You can change the id strategy:

```cs
mbb.WithProviderPostgreSql(cfg =>
{
    cfg.ConnectionString = "...";
    cfg.IdGeneration.Mode = PostgreSqlMessageIdGenerationMode.DatabaseRandomUuid;
});
```

Available modes:

- `ClientGuidGenerator` - client-side UUID generation. Defaults to `PostgreSqlSequentialGuidGenerator`, but `GuidGenerator` or `GuidGeneratorType` can be replaced.
- `DatabaseRandomUuid` - uses PostgreSQL `gen_random_uuid()`.

## How it works

- A messages table stores exchanged messages.
- A subscriptions table stores durable topic subscriptions configured by consumers.
- Queue consumers compete for rows using `FOR UPDATE SKIP LOCKED`.
- Topic publishes create one row per configured subscription.
- Message rows use a `bigserial` physical key for insert locality and a logical `uuid` message id.
- The default client-side id generator is sequential-ish for index locality. Random database ids can be selected through `cfg.IdGeneration`.
- Producers optionally call `pg_notify` after inserting messages. Polling remains the correctness mechanism.

### Polling and locking

Consumers poll the shared message table in batches. PostgreSQL uses `FOR UPDATE SKIP LOCKED` so competing consumers can skip rows already locked by another instance.

When a consumer locks a row, the transport stores the consumer instance id and lock expiration. If the process stops before completing the message, the row becomes visible again after `LockDuration`.

`pg_notify` is used as a lightweight wake-up hint when `NotifyOnPublish` is enabled. It is not used as the durable delivery mechanism; message rows in the database remain the source of truth.

### Retries and failed messages

Successful processing marks the row as complete. Failed processing increments `delivery_attempt`, clears the lock, and makes the row available for another attempt. Once `MaxDeliveryAttempts` is reached, the row is marked aborted and will no longer be delivered.

The transport retries transient PostgreSQL errors around schema provisioning and operations according to `SchemaCreationRetry` and `OperationRetry`.

### Schema provisioning

The provider provisions the required message, subscription, and migration tables during bus startup. All cooperating services should use the same database, schema, and table names.

## Testing locally

The integration tests use Testcontainers and require Docker to be running:

```bash
dotnet test src/Tests/SlimMessageBus.Host.PostgreSql.Test/SlimMessageBus.Host.PostgreSql.Test.csproj --filter "Category=Integration"
```

The repository also contains `infrastructure.ps1` for standing up shared development infrastructure used by broader integration test runs.
