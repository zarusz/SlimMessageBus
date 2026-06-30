# SQL transport provider for SlimMessageBus <!-- omit in toc -->

Please read the [Introduction](intro.md) before reading this provider documentation.

- [About](#about)
- [SQL Compatibility](#sql-compatibility)
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

The SQL transport provider allows to leverage a single shared SQL database instance as a messaging broker for all the collaborating producers and consumers.

This transport might be optimal for simpler applications that do not have a dedicated messaging infrastructure available, do not have high throughput needs, or want to target a simplistic deployment model.

When the application grows over time, and given that SMB is an abstraction, the migration from SQL towards a dedicated messaging system should be super easy.

## SQL Compatibility

This transport targets SQL Server / Azure SQL (T-SQL).

## Configuration

Install the transport package:

```bash
dotnet add package SlimMessageBus.Host.Sql
```

The configuration is arranged via the `.WithProviderSql(cfg => {})` method on the message bus builder.

```cs
using SlimMessageBus.Host.Sql;

services.AddSlimMessageBus(mbb =>
{
    mbb.WithProviderSql(cfg =>
    {
       cfg.ConnectionString = "...";
       cfg.DatabaseSchemaName = "smb";
       cfg.DatabaseTableName = "Messages";
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

- `ConnectionString` - required SQL Server or Azure SQL connection string.
- `DatabaseSchemaName` - schema containing the transport tables. Defaults to `dbo`.
- `DatabaseTableName` - base message table name, for example `Messages`. The durable subscription table uses the same base name with a `Subscriptions` suffix.
- `DatabaseMigrationsTableName` - table used to track transport schema migrations.
- `CommandTimeout` - optional SQL command timeout.
- `TransactionIsolationLevel` - isolation level used by transport transactions. Defaults to `ReadCommitted`.
- `PollDelay` - delay used when no message is available or after a transient polling error.
- `PollBatchSize` - maximum number of messages locked by one polling operation.
- `LockDuration` - how long a message lock is held before another consumer may pick it up.
- `MaxDeliveryAttempts` - number of processing attempts before a message is marked aborted.
- `SchemaCreationRetry` and `OperationRetry` - retry settings for schema creation and regular database operations.

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

Request/response endpoints can also use SQL queues or topics:

```cs
mbb.Handle<PingRequest, PingResponse>(x => x.Queue("ping-handler"));
mbb.ExpectRequestResponses(x => x.ReplyToQueue("replies"));
```

### Message id generation

The transport stores messages with two identifiers:

- `SequenceId` - a clustered `bigint identity` physical key used for insert locality and ordered polling.
- `Id` - a logical `uniqueidentifier` message id used by the transport when completing or failing messages.

By default, SQL uses `SqlMessageIdGenerationMode.ClientGuidGenerator` with `SqlSequentialGuidGenerator`, which creates sequential-ish GUIDs client-side for better index locality than random GUIDs.

You can change the id strategy:

```cs
mbb.WithProviderSql(cfg =>
{
    cfg.ConnectionString = "...";
    cfg.IdGeneration.Mode = SqlMessageIdGenerationMode.DatabaseGeneratedSequentialGuid;
});
```

Available modes:

- `ClientGuidGenerator` - client-side GUID generation. Defaults to `SqlSequentialGuidGenerator`, but `GuidGenerator` or `GuidGeneratorType` can be replaced.
- `DatabaseGeneratedGuid` - uses `NEWID()`.
- `DatabaseGeneratedSequentialGuid` - uses the table default, which is intended for SQL Server sequential GUID generation.

## How it works

The same SQL database instance is required for all the producers and consumers to collaborate.
Therefore ensure all of the service instances point to the same database cluster.

- A messages table is used to store exchanged messages.
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

### Polling and locking

Consumers poll the shared message table in batches. SQL Server locking hints (`ROWLOCK`, `UPDLOCK`, `READPAST`) are used so competing consumers can skip rows already locked by another instance.

When a consumer locks a row, the transport stores the consumer instance id and lock expiration. If the process stops before completing the message, the row becomes visible again after `LockDuration`.

### Retries and failed messages

Successful processing marks the row as complete. Failed processing increments `DeliveryAttempt`, clears the lock, and makes the row available for another attempt. Once `MaxDeliveryAttempts` is reached, the row is marked aborted and will no longer be delivered.

The transport retries transient SQL errors around schema provisioning and operations according to `SchemaCreationRetry` and `OperationRetry`.

### Schema provisioning

The provider provisions the required message, subscription, and migration tables during bus startup. All cooperating services should use the same database, schema, and table names.

## Testing locally

The integration tests use Testcontainers and require Docker to be running:

```bash
dotnet test src/Tests/SlimMessageBus.Host.Sql.Test/SlimMessageBus.Host.Sql.Test.csproj --filter "Category=Integration"
```

The repository also contains `infrastructure.ps1` for standing up shared development infrastructure used by broader integration test runs.
