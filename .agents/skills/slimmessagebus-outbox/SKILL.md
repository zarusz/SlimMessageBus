---
name: slimmessagebus-outbox
description: Configure and verify a SlimMessageBus transactional outbox. Use to publish events atomically with database changes using SQL Server, PostgreSQL, or MongoDB, or to investigate outbox transaction and delivery behavior.
---

# Configure the transactional outbox

1. Identify the database and transaction that commit the business change. Select the matching outbox package for the installed SlimMessageBus version. For SQL Server or PostgreSQL, use `AddOutboxUsingDbContext<TDbContext>(...)` with EF Core or `AddOutboxUsingSql(...)` or `AddOutboxUsingPostgreSql(...)` with a direct connection. MongoDB uses `AddOutboxUsingMongoDb(...)` and an `IMongoClient` registered in DI. The outbox storage and message transport are separate choices.
2. Ensure the business write and outbox insert share the transaction. Publish while that transaction is active and use the same provider-specific connection, EF Core context, or MongoDB session as required by the integration. Reuse an existing application transaction when possible. When SlimMessageBus should own the consumer or handler transaction, choose the documented extension for that provider and verify the handler's data access participates.
3. Put `UseOutbox()` on the producer declarations that need deferred publication. Use bus-level configuration if all applicable publishers on that bus should inherit it. The forwarding interceptor runs on `Publish`. Keep `Send` on its request/reply path.
4. Check schema or collection provisioning, delivery polling, and cleanup for the selected provider. All active instances that can drain the outbox need compatible message registrations and serialization. If the application needs ordering, check the provider's sequencing setting and what it guarantees.

Verify that a successful transaction persists the business change and delivers the event, and that a rollback leaves neither the change nor a publishable event. When testing recovery, use a test broker to verify that a pending event is sent once the broker returns. Design downstream handlers to tolerate duplicate delivery.

[SQL Server and PostgreSQL](https://github.com/zarusz/SlimMessageBus/blob/master/docs/plugin_outbox.md) · [MongoDB](https://github.com/zarusz/SlimMessageBus/blob/master/docs/plugin_outbox_mongodb.md) · [EF Core sample](https://github.com/zarusz/SlimMessageBus/blob/master/src/Samples/Sample.OutboxWebApi/Program.cs)
