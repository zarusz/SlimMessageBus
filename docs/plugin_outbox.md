# Transactional Outbox Plugin for SlimMessageBus <!-- omit in toc -->

Please read the [Introduction](intro.md) before reading this provider documentation.

- [Introduction](#introduction)
- [Configuration](#configuration)
  - [Entity Framework](#entity-framework)
  - [SQL Connection](#sql-connection)
- [Options](#options)
  - [UseOutbox for Producers](#useoutbox-for-producers)
  - [Transactions for Consumers](#transactions-for-consumers)
    - [UseTransactionScope](#usetransactionscope)
    - [UseSqlTransaction](#usesqltransaction)
- [How it works](#how-it-works)

## Introduction

The [`Host.Outbox`](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox) introduces [Transactional Outbox](https://microservices.io/patterns/data/transactional-outbox.html) pattern to the SlimMessageBus.
It comes in two flavors:

- [`Host.Outbox.Sql`](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.Sql) as integration with the System.Data.Sql client
- [`Host.Outbox.DbContext`](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.DbContext) as integration with Entity Framework Core

Outbox plugin can work in combination with any transport provider.

## Configuration

### Entity Framework

> Required: [`SlimMessageBus.Host.Outbox.DbContext`](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.DbContext)

```cs
using SlimMessageBus.Host.Outbox.DbContext;
```

Consider the following example (from [Samples](../src/Samples/Sample.OutboxWebApi/Program.cs)):

- `services.AddOutboxUsingDbContext<CustomerContext>(...)` is used to add the [Outbox.DbContext](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.DbContext) plugin to the container.
- `CustomerContext` is the application specific Entity Framework `DbContext`.
- `CustomerCreatedEvent` is produced on the `AzureSB` child bus, the bus will deliver these events via outbox - see `.UseOutbox()`
- `CreateCustomerCommand` is consumed on the `Memory` child bus, each command is wrapped in an SQL transaction - see `UseSqlTransaction()`

Startup setup:

```cs
builder.Services.AddSlimMessageBus(mbb =>
{
    mbb
        .AddChildBus("Memory", mbb =>
        {
            mbb.WithProviderMemory()
                .AutoDeclareFrom(Assembly.GetExecutingAssembly(), consumerTypeFilter: t => t.Name.Contains("Command"))
                //.UseTransactionScope(); // Consumers/Handlers will be wrapped in a TransactionScope
                .UseSqlTransaction(); // Consumers/Handlers will be wrapped in a SqlTransaction
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
                .UseOutbox(); // All outgoing messages from this bus will go out via an outbox
        })
        .AddServicesFromAssembly(Assembly.GetExecutingAssembly())
        .AddJsonSerializer()
        .AddAspNet()
        .AddOutboxUsingDbContext<CustomerContext>(opts =>
        {
            opts.PollBatchSize = 100;
            opts.MessageCleanup.Interval = TimeSpan.FromSeconds(10);
            opts.MessageCleanup.Age = TimeSpan.FromMinutes(1);
            //opts.SqlSettings.TransactionIsolationLevel = System.Data.IsolationLevel.RepeatableRead;
            //opts.SqlSettings.Dialect = SqlDialect.SqlServer;
        });
});
```

Command handler:

```cs
public record CreateCustomerCommandHandler(IMessageBus Bus, CustomerContext CustomerContext) : IRequestHandler<CreateCustomerCommand, Guid>
{
    public async Task<Guid> OnHandle(CreateCustomerCommand request)
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

### SQL Connection

> Required: [`SlimMessageBus.Host.Outbox.Sql`](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.Sql)

```cs
using SlimMessageBus.Host.Outbox.Sql;
```

Consider the following example:

- `services.AddMessageBusOutboxUsingSql(...)` is used to add the [Outbox.Sql](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.Sql) plugin to the container.
- `SqlConnection` is registered in the container

```cs
builder.Services.AddSlimMessageBus(mbb =>
{
    // Alternatively, if we were not using EF, we could use a SqlConnection
    mbb.AddOutboxUsingSql(opts => { opts.PollBatchSize = 100; });
});

// SMB requires the SqlConnection to be registered in the container
builder.Services.AddTransient(svp =>
{
    var configuration = svp.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("DefaultConnection");
    return new SqlConnection(connectionString);
});
```

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

> Required: [`Host.Outbox`](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox)

```cs
using SlimMessageBus.Host.Outbox.Sql;
```

`.UseTransactionScope()` can be used on consumers (or handlers) declaration to force the consumer to start a `TransactionScope` prior the message `OnHandle` and to complete that transaction after it. Any exception raised by the consumer would cause the transaction to be rolled back.

When applied on the (child) bus level then all consumers (or handlers) will inherit that option.

#### UseSqlTransaction

> Required: [`SlimMessageBus.Host.Outbox.Sql`](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.Sql) or [`SlimMessageBus.Host.Outbox.DbContext`](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.DbContext)

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

- When the message publication happens in the context of a consumer (or handler) of another message, the `.UseTransactionScope()`, `.UseSqlTransaction()` can be used to start a transaction.

- The transaction can be managed by the application, starting it either explicitly using `DbContext.Database.BeginTransactionAsync()` or creating a `TransactionScope()`.

- On the interceptor being disposed (at then end of a message lifecycle), the outbox service will be notified that a message is waiting to be published if at least one messages was placed in the outbox during the message processing lifetime. Alternatively, on expiry of the `PollIdleSleep` period following outbox processing, the same process will be initiated.

- If the configuration setting `MaintainSequence` is set to `true`, only one application instance will be able to lock messages for delivery. This ensure messages are delivered in the original order to the service bus at the expense of delivery throughput. If `false`, each distributed instance will place an exclusive lock on `PollBatchSize` messages for concurrent distribution. This will greatly increase throughput at the expense of the FIFO sequence.

- If a service instance where to crash or restart, the undelivered messages will be picked and locked by another instance.

- Once a message is picked from outbox and successfully delivered then it is marked as sent in the outbox table.

- At configured intervals (`MessageCleanup.Interval`), and after a configured time span (`MessageCleanup.Age`), the sent messages are removed from the outbox table.