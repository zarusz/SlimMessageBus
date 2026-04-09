# Transactional Outbox Plugin for MongoDB <!-- omit in toc -->

Please read the [Introduction](intro.md) and the [Transactional Outbox](plugin_outbox.md) overview before reading this page.

- [Introduction](#introduction)
- [Configuration](#configuration)
- [Options](#options)
  - [UseOutbox for Producers](#useoutbox-for-producers)
  - [UseMongoDbTransaction for Consumers](#usemongodbtransaction-for-consumers)
- [How it works](#how-it-works)
- [Collections](#collections)
- [Migration versioning](#migration-versioning)
- [Indices](#indices)
- [Clean up](#clean-up)
- [Important note](#important-note)

## Introduction

[`SlimMessageBus.Host.Outbox.MongoDb`](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.MongoDb) adds [Transactional Outbox](https://microservices.io/patterns/data/transactional-outbox.html) pattern support backed by **MongoDB**.

It uses the [MongoDB.Driver](https://www.nuget.org/packages/MongoDB.Driver) (3.x) and targets **.NET 8 and .NET 10**.

> Requires `IMongoClient` to be registered in the DI container.

## Configuration

> Required: [`SlimMessageBus.Host.Outbox.MongoDb`](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.MongoDb)

```bash
dotnet add package SlimMessageBus.Host.Outbox.MongoDb
```

Call `.AddOutboxUsingMongoDb()` on the `MessageBusBuilder` to enable the plugin:

```csharp
using SlimMessageBus.Host.Outbox.MongoDb;

builder.Services.AddSlimMessageBus(mbb =>
{
    mbb
        .AddChildBus("Memory", mbb =>
        {
            mbb.WithProviderMemory()
               .AutoDeclareFrom(Assembly.GetExecutingAssembly(), consumerTypeFilter: t => t.Name.EndsWith("CommandHandler"))
               // Wrap each command handler in a MongoDB multi-document transaction
               .UseMongoDbTransaction(messageTypeFilter: t => t.Name.EndsWith("Command"));
        })
        .AddChildBus("AzureSB", mbb =>
        {
            mbb.WithProviderServiceBus(cfg => { /* ... */ })
               .Produce<CustomerCreatedEvent>(x => x.DefaultTopic("samples.outbox/customer-events"))
               // All outgoing messages from this bus will go out via the outbox
               .UseOutbox();
        })
        .AddServicesFromAssembly(Assembly.GetExecutingAssembly())
        .AddJsonSerializer()
        // Configure MongoDB outbox
        .AddOutboxUsingMongoDb(opts =>
        {
            opts.PollBatchSize = 500;
            opts.PollIdleSleep = TimeSpan.FromSeconds(10);
            opts.MessageCleanup.Interval = TimeSpan.FromSeconds(60);
            opts.MessageCleanup.Age = TimeSpan.FromMinutes(60);
            // Override MongoDB collection names (optional)
            // opts.MongoDbSettings.DatabaseName = "myapp";
            // opts.MongoDbSettings.CollectionName = "smb_outbox";
            // opts.MongoDbSettings.LockCollectionName = "smb_outbox_lock";
        });
});

// SMB requires IMongoClient to be registered in the container
builder.Services.AddSingleton<IMongoClient>(new MongoClient(connectionString));
```

## Options

### UseOutbox for Producers

`.UseOutbox()` marks a producer (or an entire child bus) to route outgoing messages through the outbox instead of publishing them directly to the transport.

```csharp
mbb.Produce<OrderCreatedEvent>(x =>
{
    x.DefaultTopic("order-events");
    x.UseOutbox(); // this producer uses the outbox
});

// or for all producers on a bus:
mbb.UseOutbox();
```

### UseMongoDbTransaction for Consumers

`.UseMongoDbTransaction()` wraps each consumer (or handler) in a MongoDB multi-document transaction. The transaction is committed after a successful `OnHandle` call and rolled back on any exception.

> **Note:** MongoDB multi-document transactions require a **replica set** (or sharded cluster). Standalone `mongod` instances do not support transactions.

```csharp
using SlimMessageBus.Host.Outbox.MongoDb;

// On a single consumer:
mbb.Consume<CreateCustomerCommand>(x =>
    x.WithConsumer<CreateCustomerCommandHandler>()
     .UseMongoDbTransaction());

// Or across all consumers on a bus:
mbb.UseMongoDbTransaction(messageTypeFilter: t => t.Name.EndsWith("Command"));
```

#### Enlisting your own MongoDB writes in the transaction

The **outbox insert** always participates in the active transaction automatically. However, unlike SQL (where a `SqlConnection` carries the transaction implicitly), MongoDB requires the `IClientSessionHandle` to be **passed explicitly** to every collection operation.

To make your own document writes atomic with the outbox insert, inject `IClientSessionHandle?` directly into the consumer constructor:

```csharp
// No dependency on SlimMessageBus.Host.Outbox.MongoDb — only MongoDB.Driver types needed.
public class CreateCustomerCommandHandler(
    IMongoCollection<Customer> customers,
    IClientSessionHandle? session,   // null when no transaction is active
    IMessageBus bus) : IRequestHandler<CreateCustomerCommand, Guid>
{
    public async Task<Guid> OnHandle(CreateCustomerCommand request, CancellationToken ct)
    {
        var customer = new Customer(request.Name);

        // Both writes share the same session — committed or rolled back together.
        if (session != null)
            await customers.InsertOneAsync(session, customer, cancellationToken: ct);
        else
            await customers.InsertOneAsync(customer, cancellationToken: ct);

        // This publish goes via the outbox and is in the same transaction.
        await bus.Publish(new CustomerCreatedEvent(customer.Id));
        return customer.Id;
    }
}
```

> **Why does constructor injection work here?**  
> SMB resolves the consumer from DI *after* all interceptors have executed. `MongoDbTransactionConsumerInterceptor` starts the session before the consumer is constructed, so the DI factory for `IClientSessionHandle` already finds a live session in `MongoDbSessionHolder` by the time the consumer's constructor runs. See [Consumer instance resolution order](intro.md#consumer-instance-resolution-order) for the full execution diagram.

`session` is `null` when no transaction is active (e.g. `UseMongoDbTransaction()` is not configured, or running against a standalone `mongod`). The `null` check makes the consumer work in both cases.

## How it works

- On bus start, `MongoDbOutboxMigrationService` creates the outbox collection and lock collection (if they do not exist) together with the supporting indices.
- When a message is published via a producer marked with `.UseOutbox()`, the message is inserted into the outbox MongoDB collection.
  - If the call happens inside a consumer that has `.UseMongoDbTransaction()` enabled, the insert participates in the active MongoDB session, ensuring atomicity with any other writes performed during that consumer invocation.
- A background poller periodically locks a batch of undelivered messages (up to `PollBatchSize`) and forwards them to the actual transport. Locking is done in two steps:
  1. Find candidate document IDs (ordered by `Timestamp`, limited to `PollBatchSize`).
  2. Atomically claim them with an `UpdateMany` that re-applies the eligibility filter to handle concurrent instances.
- When `MaintainSequence = true`, an additional global lock document (in the lock collection) ensures only one application instance processes the outbox at a time, preserving message order at the cost of throughput.
- After successful delivery each document is marked `DeliveryComplete = true`. On repeated failures the `DeliveryAttempt` counter is incremented; once it reaches `MaxDeliveryAttempts` the document is marked `DeliveryAborted = true` and skipped.

## Collections

By default three MongoDB collections are used:

| Collection              | Setting                                    | Default                  |
| ----------------------- | ------------------------------------------ | ------------------------ |
| Outbox messages         | `MongoDbSettings.CollectionName`           | `smb_outbox`             |
| Global lock (table-lock mode) | `MongoDbSettings.LockCollectionName` | `smb_outbox_lock`        |
| Applied migrations      | `MongoDbSettings.MigrationsCollectionName` | `smb_outbox_migrations`  |

The database is set via `MongoDbSettings.DatabaseName` (default: `slimmessagebus`).

## Migration versioning

Schema changes are tracked in the `smb_outbox_migrations` collection. Each migration step has a unique timestamp-based ID (e.g. `"20240101000000_SMB_Init"`). On startup `MongoDbOutboxMigrationService` checks whether each migration ID is present in the collection:

- **Not present** → the action (index creation/modification) runs and the ID is recorded on success.
- **Present** → skipped.

This gives **at-least-once** (not exactly-once) execution semantics:

- A crash before the record is written → **retried on the next startup** (safe, all actions are idempotent).
- Two instances racing simultaneously → both may run the action, one wins the insert race, the other handles the `DuplicateKey` exception (safe for idempotent actions).

> **Note:** MongoDB does not allow DDL operations such as `createIndex` inside multi-document transactions. Migrations are therefore intentionally **not transactional** — safety comes from idempotency, not atomicity. Only add migration steps that are safe to run more than once (i.e. index creation using `IF NOT EXISTS` semantics). Destructive one-shot operations must be applied externally with `EnableMigration = false`.

To add a future migration, append a new `TryApplyMigration` call in the service with a new unique ID. Old migration IDs must never be reused.

### Disabling migrations

Set `MongoDbSettings.EnableMigration = false` to skip the entire migration step at startup. Use this when you manage schema changes externally (e.g. via a deployment pipeline) and want SMB to leave the database schema untouched.

```csharp
.AddOutboxUsingMongoDb(opts =>
{
    opts.MongoDbSettings.EnableMigration = false;
});
```

## Indices

`MongoDbOutboxMigrationService` ensures the following indices exist on startup:

**Outbox collection (`smb_outbox`)**

| Index fields                                        | Purpose                          |
| --------------------------------------------------- | -------------------------------- |
| `delivery_complete`, `delivery_aborted`, `timestamp` | Main polling query               |
| `lock_instance_id`, `lock_expires_on`               | Lock-ownership queries           |
| `timestamp`                                         | Cleanup (delete-sent) ordering   |

**Lock collection (`smb_outbox_lock`)**

| Index fields     | Purpose               |
| ---------------- | --------------------- |
| `lock_expires_on` | Expired-lock detection |

## Clean up

Sent messages older than `MessageCleanup.Age` are removed in batches of `MessageCleanup.BatchSize` on startup and then every `MessageCleanup.Interval`.

| Property  | Description                                        | Default |
| --------- | -------------------------------------------------- | ------- |
| Enabled   | `true` if sent messages are to be removed          | true    |
| Interval  | Time between clean-up executions                   | 1 hour  |
| Age       | Minimum age of a sent message to delete            | 1 hour  |
| BatchSize | Number of messages to be removed in each iteration | 10 000  |

## Important note

Because the outbox can be processed by any application instance, all active instances must share the same message registrations and compatible serialization schema.

A message that fails to be delivered will have its `DeliveryAborted` flag set to `true` in the outbox collection once `MaxDeliveryAttempts` is exceeded. It is safe to reset this flag to `false` manually (e.g. via `mongosh`) once the underlying issue has been resolved.
