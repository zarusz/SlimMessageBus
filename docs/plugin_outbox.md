# Transactional Outbox Plugin for SlimMessageBus <!-- omit in toc -->

Please read the [Introduction](intro.md) before reading this provider documentation.

- [Introduction](#introduction)
- [Configuration](#configuration)
  - [Entity Framework](#entity-framework)
    - [PostgreSQL](#postgresql)
    - [SQL Server](#sql-server)
  - [Direct Connection](#direct-connection)
    - [PostgreSQL](#postgresql-1)
    - [SQL Server](#sql-server-1)
  - [SQL Server authentication and reconnect recovery](#sql-server-authentication-and-reconnect-recovery)
- [Options](#options)
  - [UseOutbox for Producers](#useoutbox-for-producers)
  - [Transactions for Consumers](#transactions-for-consumers)
    - [UseTransactionScope](#usetransactionscope)
    - [UsePostgreSqlTransaction](#usepostgresqltransaction)
    - [UseSqlTransaction](#usesqltransaction)
- [How it works](#how-it-works)
- [Clean up](#clean-up)
- [Important note](#important-note)

## Introduction

The [`Host.Outbox`](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox) introduces [Transactional Outbox](https://microservices.io/patterns/data/transactional-outbox.html) pattern to the SlimMessageBus.

PostgreSQL
- [`Host.Outbox.PostgreSql`](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.PostgreSql) as integration with the [Npgsql](https://www.npgsql.org/) client
- [`Host.Outbox.PostgreSql.DbContext`](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.PostgreSql.DbContext) as integration with Entity Framework Core using Npgsql

SQL server
- [`Host.Outbox.Sql`](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.Sql) as integration with the System.Data.Sql client (MSSQL)
- [`Host.Outbox.Sql.DbContext`](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.Sql.DbContext) as integration with Entity Framework Core using System.Data.Sql

Outbox plugin can work in combination with any transport provider.

The outbox provisions its database schema during bus initialization. SQL Server schema migration retries use a fresh transaction after a rollback. Concurrent initialization calls share the migration attempt, and successful migrations are cached. A failed migration can run again on a subsequent publish or bus start, so fixing a database permission or availability problem does not require recreating the bus. The database account must have the permissions required to create and update the outbox schema.

## Configuration

### Entity Framework
#### PostgreSQL

> Required: [`SlimMessageBus.Host.Outbox.PostgreSql.DbContext`](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.PostgreSql.DbContext)

```cs
using SlimMessageBus.Host.Outbox.PostgreSql.DbContext;
```

#### SQL Server

> Required: [`SlimMessageBus.Host.Outbox.Sql.DbContext`](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.Sql.DbContext)

```cs
using SlimMessageBus.Host.Outbox.Sql.DbContext;
```

Consider the following example (from [Samples](../src/Samples/Sample.OutboxWebApi/Program.cs)):

- `services.AddOutboxUsingDbContext<CustomerContext>(...)` is used to add the [Outbox.DbContext](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.Sql.DbContext) plugin to the container.
- `CustomerContext` is the application specific Entity Framework `DbContext`.
- `CustomerCreatedEvent` is produced on the `AzureSB` child bus, the bus will deliver these events via outbox - see `.UseOutbox()`
- `CreateCustomerCommand` is consumed on the `Memory` child bus, each command is wrapped in an SQL transaction - see `UsePostgreSqlTransaction()` / `UseSqlTransaction()`

Startup setup:

```cs
builder.Services.AddSlimMessageBus(mbb =>
{
    mbb.PerMessageScopeEnabled(false);
    mbb
        .AddChildBus("Memory", mbb =>
        {
            mbb.WithProviderMemory()
                .AutoDeclareFrom(Assembly.GetExecutingAssembly(), consumerTypeFilter: t => t.Name.EndsWith("CommandHandler"));
            //.UseTransactionScope(messageTypeFilter: t => t.Name.EndsWith("Command")) // Consumers/Handlers will be wrapped in a TransactionScope
            //.UseSqlTransaction(messageTypeFilter: t => t.Name.EndsWith("Command")); // Consumers/Handlers will be wrapped in a SqlTransaction ending with Command

            switch (dbProvider)
            {
                case DbProvider.SqlServer:
                    mbb.UseSqlTransaction(messageTypeFilter: t => t.Name.EndsWith("Command")); // Consumers/Handlers will be wrapped in a SqlTransaction ending with Command
                    break;

                case DbProvider.PostgreSql:
                    mbb.UsePostgreSqlTransaction(messageTypeFilter: t => t.Name.EndsWith("Command")); // Consumers/Handlers will be wrapped in a SqlTransaction ending with Command
                    break;
            }
        })
        .AddChildBus("AzureSB", mbb =>
        {
            mbb
                .Handle<CreateCustomerCommand, Guid>(s =>
                {
                    s.Topic("samples.outbox/customer-events", t =>
                    {
                        t.WithHandler<CreateCustomerCommandHandler, CreateCustomerCommand>()
                            .SubscriptionName("CreateCustomer");
                    });
                })
                .WithProviderServiceBus(cfg =>
                {
                    cfg.ConnectionString = Secrets.Service.PopulateSecrets(configuration["Azure:ServiceBus"]);
                    cfg.TopologyProvisioning.CanProducerCreateTopic = true;
                    cfg.TopologyProvisioning.CanConsumerCreateQueue = true;
                    cfg.TopologyProvisioning.CanConsumerReplaceSubscriptionFilters = true;
                })
                .Produce<CustomerCreatedEvent>(x =>
                {
                    x.DefaultTopic("samples.outbox/customer-events");
                    // OR if you want just this producer to sent via outbox
                    // x.UseOutbox();
                })
                // All outgoing messages from this bus will go out via an outbox
                .UseOutbox(/* messageTypeFilter: t => t.Name.EndsWith("Command") */); // Additionally, can apply filter do determine messages that should go out via outbox                
        })
        .AddServicesFromAssembly(Assembly.GetExecutingAssembly())
        .AddJsonSerializer()
        .AddAspNet();

    switch (dbProvider)
    {
        case DbProvider.SqlServer:
            SlimMessageBus.Host.Outbox.Sql.DbContext.MessageBusBuilderExtensions.AddOutboxUsingDbContext<CustomerContext>(mbb, opts =>
            {
                opts.PollBatchSize = 500;
                opts.PollIdleSleep = TimeSpan.FromSeconds(10);
                opts.MessageCleanup.Interval = TimeSpan.FromSeconds(10);
                opts.MessageCleanup.Age = TimeSpan.FromMinutes(1);
                //opts.SqlSettings.TransactionIsolationLevel = System.Data.IsolationLevel.RepeatableRead;
                //opts.SqlSettings.Dialect = SqlDialect.SqlServer;
            });

            break;

        case DbProvider.PostgreSql:
            SlimMessageBus.Host.Outbox.PostgreSql.DbContext.MessageBusBuilderExtensions.AddOutboxUsingDbContext<CustomerContext>(mbb, opts =>
            {
                opts.PollBatchSize = 500;
                opts.PollIdleSleep = TimeSpan.FromSeconds(10);
                opts.MessageCleanup.Interval = TimeSpan.FromSeconds(10);
                opts.MessageCleanup.Age = TimeSpan.FromMinutes(1);
                //opts.SqlSettings.TransactionIsolationLevel = System.Data.IsolationLevel.RepeatableRead;
                //opts.SqlSettings.Dialect = SqlDialect.SqlServer;
            });

            break;
    }
});
```

Command handler:

```cs
public record CreateCustomerCommandHandler(IMessageBus Bus, CustomerContext CustomerContext) : IRequestHandler<CreateCustomerCommand, Guid>
{
    public async Task<Guid> OnHandle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        // Note: This handler will be already wrapped in a transaction: see Program.cs and .UseTransactionScope() / .UseSqlTransaction() 

        var customer = new Customer(request.Firstname, request.Lastname);
        await CustomerContext.Customers.AddAsync(customer);
        await CustomerContext.SaveChangesAsync();

        // Announce to anyone outside of this micro-service that a customer has been created (this will go out via an transactional outbox)
        await Bus.Publish(new CustomerCreatedEvent(customer.Id, customer.Firstname, customer.Lastname));

        return customer.Id;
    }
}
```

### Direct Connection
#### PostgreSQL

> Required: [`SlimMessageBus.Host.Outbox.PostgreSql`](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.PostgreSql)

```cs
using SlimMessageBus.Host.Outbox.PostgreSql;
```

#### SQL Server

> Required: [`SlimMessageBus.Host.Outbox.Sql`](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.Sql)

```cs
using SlimMessageBus.Host.Outbox.Sql;
```

Consider the following example:

- `services.AddOutboxUsingSql(...)` is used to add the [Outbox.Sql](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.Sql) plugin to the container.
- `SqlConnection` is registered in the container

```cs
builder.Services.AddSlimMessageBus(mbb =>
{
    // Alternatively, if we were not using EF, we could use a SqlConnection
    mbb.AddOutboxUsingSql(opts => { opts.PollBatchSize = 100; });
});

// SMB requires the SqlConnection to be registered in the container
builder.Services.AddScoped(svp =>
{
    var configuration = svp.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("DefaultConnection");
    return new SqlConnection(connectionString);
});
```

### SQL Server authentication and reconnect recovery

For Azure SQL, prefer SqlClient's built-in Microsoft Entra authentication instead of assigning an access token once in a connection factory. SqlClient acquires and renews tokens when establishing connections. Keep the `SqlConnection` (or EF `DbContext`) registration scoped, and let connection pooling reuse physical connections.

On .NET 8 and .NET 10, the SQL outbox currently uses Microsoft.Data.SqlClient 7.0.2. Applications using its built-in Entra authentication modes must also reference the matching Azure extension package:

```xml
<PackageReference Include="Microsoft.Data.SqlClient.Extensions.Azure" Version="7.0.2" />
```

Keep the extension and driver versions aligned if the application overrides the SqlClient version; see the [7.0.2 package alignment notes](https://github.com/dotnet/SqlClient/blob/main/release-notes/7.0/7.0.2.md). SqlClient 6.x, used by the older target frameworks, includes these authentication providers in the driver itself.

For a system-assigned managed identity exposed by the Azure host, configure the application's connection string as follows:

```text
Server=tcp:<server>.database.windows.net,1433;Database=<database>;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;
```

For a user-assigned managed identity, add `User ID=<managed-identity-client-id>`. For an AKS pod configured with Microsoft Entra Workload ID federation, use:

```text
Server=tcp:<server>.database.windows.net,1433;Database=<database>;Authentication=Active Directory Workload Identity;Encrypt=True;TrustServerCertificate=False;
```

Workload identity requires `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, and `AZURE_FEDERATED_TOKEN_FILE` in the pod. The identity must have database access and the permissions required for outbox schema provisioning. Authentication mode alone does not configure the Azure host, federation, or database permissions. See [Microsoft's authentication guide](https://learn.microsoft.com/en-us/sql/connect/ado-net/sql/azure-active-directory-authentication) and the [SqlClient 7.0 authentication package change](https://github.com/dotnet/SqlClient/blob/main/release-notes/7.0/7.0.0.md).

Use the same connection registration shown above; do not also assign `AccessToken` or `AccessTokenCallback` when using a built-in authentication mode. If custom token acquisition is required, use a stable `AccessTokenCallback` that returns the token and its expiration, rather than a fixed `AccessToken` string. Do not assume a fixed token lifetime.

The outbox sender creates a new DI scope for each polling cycle and disposes it before waiting for the next poll. Its lock-renewal factory is scoped too, with a separate connection for renewal; asynchronous renewal completes before the scopes are disposed. After a poll or repository-resolution failure, the next poll resolves fresh services without restarting the bus. Failures and subsequent recovery are logged. Register custom connection factories and repositories so they return fresh scoped instances rather than a singleton connection.

SQL connection opening uses `SqlSettings.OperationRetry` for bounded retries of qualifying connection failures, handles broken connections by closing them before reopening, and honors the operation's cancellation token. `RetryCount` counts total attempts and must be at least one. Invalid-login (18456) and permission-denied (229) errors are propagated without connection retries; the next polling cycle can recover after the underlying configuration or permission issue is corrected.

Connection recovery does not resume an interrupted transaction or replay an operation whose result is unknown. A disconnected active transaction fails and must be retried as a complete unit of work by its owner. The existing command retry policy is unchanged. Outbox delivery remains at least once: a disconnect after publishing but before recording delivery can cause redelivery, so consumers should tolerate duplicates.

## Options

### UseOutbox for Producers

> Required: [`SlimMessageBus.Host.Outbox`](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox)

```cs
using SlimMessageBus.Host.Outbox;
```

`.UseOutbox()` can be used on producer declaration to require outgoing messages to use the outbox.
When applied on the (child) bus level then all the producers will inherit that option.

### Transactions for Consumers

Each consumer (or handler) can be placed inside of an SQL transaction. What that means is that when a consumer processes a message, an transaction will be started automatically by SMB, then if processing is successful that transaction will get committed. In the case of an error it will be rolled back.

The transactions can be nested. For example a consumer (e.g. Azure SB) invokes a command handler (e.g. Memory) and they both have transactions enabled, then the underlying transaction is committed when both consumers finish with success.

There are two types of transaction support:

#### UseTransactionScope

> Required: [`SlimMessageBus.Host.Outbox`](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox)

```cs
using SlimMessageBus.Host.Outbox;
```

`.UseTransactionScope()` can be used on consumers (or handlers) declaration to force the consumer to start a `TransactionScope` prior the message `OnHandle` and to complete that transaction after it. Any exception raised by the consumer would cause the transaction to be rolled back.

When applied on the (child) bus level then all consumers (or handlers) will inherit that option.

#### UsePostgreSqlTransaction

> Required: [`SlimMessageBus.Host.Outbox.PostgreSql`](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.PostgreSql) or [`SlimMessageBus.Host.Outbox.PostgreSql.DbContext`](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.PostgreSql.DbContext)

```cs
using SlimMessageBus.Host.Outbox.PostgreSql;
```

`.UsePostgreSqlTransaction()` can be used on consumers (or handlers) declaration to force the consumer to start a `PostgreSqlTransaction` prior the message `OnHandle` and to complete that transaction after it. Any exception raised by the consumer would cause the transaction to be rolled back.

When applied on the (child) bus level then all consumers (or handlers) will inherit that option.

`PostgreSqlTransaction`-s are created off the associated `NpgsqlConnection`.

#### UseSqlTransaction

> Required: [`SlimMessageBus.Host.Outbox.Sql`](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.Sql) or [`SlimMessageBus.Host.Outbox.Sql.DbContext`](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.Sql.DbContext)

```cs
using SlimMessageBus.Host.Outbox.Sql;
```

`.UseSqlTransaction()` can be used on consumers (or handlers) declaration to force the consumer to start a `SqlTransaction` prior the message `OnHandle` and to complete that transaction after it. Any exception raised by the consumer would cause the transaction to be rolled back.

When applied on the (child) bus level then all consumers (or handlers) will inherit that option.

`SqlTransaction`-s are created off the associated `SqlConnection`.

## How it works

- Outbox plugin uses the [interceptor](intro.md#interceptors) interface to tap into the relevant stages of message processing.

- Upon bus start the `Outbox` SQL table is created (if does not exist). The name of the table can be adjusted via settings.

- When a message is sent via a bus or producer marked with `.UseOutbox()` then such message will be inserted into the `Outbox` table.
  It is important that message publish happens in the context of an transaction to ensure consistency.

- When the message publication happens in the context of a consumer (or handler) of another message, the `.UseTransactionScope()`, `.UseSqlTransaction()` or `.UseSqlTransaction()` can be used to start a transaction.

- The transaction can be managed by the application, starting it either explicitly using `DbContext.Database.BeginTransactionAsync()` or creating a `TransactionScope()`.

- On the interceptor being disposed (at then end of a message lifecycle), the outbox service will be notified that a message is waiting to be published if at least one messages was placed in the outbox during the message processing lifetime. Alternatively, on expiry of the `PollIdleSleep` period following outbox processing, the same process will be initiated.

- If the configuration setting `MaintainSequence` is set to `true`, only one application instance will be able to lock messages for delivery. This ensure messages are delivered in the original order to the service bus at the expense of delivery throughput. If `false`, each distributed instance will place an exclusive lock on `PollBatchSize` messages for concurrent distribution. This will greatly increase throughput at the expense of the FIFO sequence.

- If a service instance where to crash or restart, the undelivered messages will be picked and locked by another instance.

- Once a message is picked from outbox and successfully delivered then it is marked as sent in the outbox table.

## Clean up
On starting SMB, messages that are older than `MessageCleanup.Age` will be removed from the `Outbox` table in batches of `MessageCleanup.BatchSize` until no sent messages of the specified age remain. The process is then repeated every `MessageCleanup.Interval` period.

| Property  | Description                                        | Default |
| --------- | -------------------------------------------------- | ------- |
| Enabled   | `True` if messages are to be removed               | true    |
| Interval  | Time between exections                             | 1 hour  |
| Age       | Minimum age of a sent message to delete            | 1 hour  |
| BatchSize | Number of messages to be removed in each iteration | 10 000  |

## Important note

As the outbox can be processed by instance of the application that did not originally process it, it is important to ensure that all active instances maintain the same message registrations (and compatible JSON schema definitions).

A message that fails to deserialize will be flagged as invalid by setting the associated `DeliveryAborted` field in the `Outbox` table, to `1`. It is safe to manually reset this field value to `0` once the version incompatibility has been resolved.