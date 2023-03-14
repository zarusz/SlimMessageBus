# Transactional Outbox Plugin for SlimMessageBus <!-- omit in toc -->

Please read the [Introduction](intro.md) before reading this provider documentation.

- [Introduction](#introduction)
- [Configuration](#configuration)
  - [Entity Framework](#entity-framework)
  - [SQL Connection](#sql-connection)
- [Options](#options)
  - [UseOutbox](#useoutbox)
  - [UseTransactionScope](#usetransactionscope)
  - [UseSqlTransaction](#usesqltransaction)
- [How it works](#how-it-works)
  
## Introduction

The [`Host.Outbox`](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox) introduces [Transactional Outbox](https://microservices.io/patterns/data/transactional-outbox.html) pattern to the SlimMessageBus. It comes in two flavors:

- [`Host.Outbox.Sql`](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.Sql) as integration with the System.Data.Sql client
- [`Host.Outbox.DbContext`](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.DbContext) as integration with Entity Framework Core

Outbox plugin can work with any transport provider.

## Configuration

### Entity Framework

> Required: [`SlimMessageBus.Host.Outbox.DbContext`](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.DbContext)

Consider the following example:

- `services.AddOutboxUsingDbContext<CustomerContext>(...)` is used to add the [Outbox.DbContext](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.DbContext) plugin to the container.
- `CustomerContext` is the application specific Entity Framework `DbContext`.
- `CustomerCreatedEvent` is produced on the `AzureSB` child bus, the bus will deliver these events via outbox - see `.UseOutbox()`
- `CreateCustomerCommand` is consumed on the `Memory` child bus, each command is wrapped in an SQL transaction - see `UseSqlTransaction()`

```cs
// Configure the Bus
builder.Services.AddSlimMessageBus(mbb =>
{
    mbb
        .WithProviderHybrid()
        .AddChildBus("Memory", mbb =>
        {
            mbb.WithProviderMemory()
               .AutoDeclareFrom(Assembly.GetExecutingAssembly(), consumerTypeFilter: t => t.Name.Contains("Command"))
               .UseSqlTransaction(); // Consumers/Handlers will be wrapped in a SqlTransaction
               //.UseTransactionScope(); // Consumers/Handlers will be wrapped in a TransactionScope
        })
        .AddChildBus("AzureSB", mbb =>
        {
            var serviceBusConnectionString = ""; // some connection string
            mbb.WithProviderServiceBus(new ServiceBusMessageBusSettings(serviceBusConnectionString))
               .Produce<CustomerCreatedEvent>(x =>
               {
                   x.DefaultTopic("samples.outbox/customer-events");
                   // OR if you want just this producer to sent via outbox
                   // x.UseOutbox();
               })
               .UseOutbox(); // All outgoing messages from this bus will go out via an outbox
        })
        .AddJsonMessageSerializer()
        .AddServicesFromAssembly(Assembly.GetExecutingAssembly())
        // Register the Outbox plugin, and let it use DbConnection and manage SqlTransaction using the CustomerContext DbContext
        .AddOutboxUsingDbContext<CustomerContext>(opts =>
        {
            opts.PollBatchSize = 100;
            //opts.TransactionIsolationLevel = System.Data.IsolationLevel.RepeatableRead;
            //opts.Dialect = SqlDialect.SqlServer;
        });
});

// Command handler for CreateCustomerCommand
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

Consider the following example:

- `services.AddMessageBusOutboxUsingSql(...)` is used to add the [Outbox.Sql](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.Sql) plugin to the container.
- `SqlConnection` is registered in the container

```cs
builder.Services.AddSlimMessageBus(mbb =>
{
    // Alternatively, if we were not using EF, we could use a SqlConnection
    mbb.AddOutboxUsingSql(opts => { opts.PollBatchSize = 100; });
});

// Register in the the container the SqlConnection
builder.Services.AddTransient(svp =>
{
    var configuration = svp.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("DefaultConnection");
    return new SqlConnection(connectionString);
});
```

## Options

### UseOutbox

> Required: [`SlimMessageBus.Host.Outbox`](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox)

`.UseOutbox()` can be used on producer declaration to require outgoing messages to use the outbox.
When applied on the (child) bus level then all the producers will inherit that option.

### UseTransactionScope

> Required: [`Host.Outbox`](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox)

`.UseTransactionScope()` can be used on consumers (or handlers) declaration to force the consumer to start a `TransactionScope` prior the message `OnHandle` and to complete that transaction after it. Any exception raised by the consumer would cause the transaction to be rolled back.

When applied on the (child) bus level then all consumers (or handlers) will inherit that option.

### UseSqlTransaction

> Required: [`SlimMessageBus.Host.Outbox.Sql`](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.Sql) or [`SlimMessageBus.Host.Outbox.DbContext`](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.DbContext)

`.UseSqlTransaction()` can be used on consumers (or handlers) declaration to force the consumer to start a `SqlTransaction`  prior the message `OnHandle` and to complete that transaction after it. Any exception raised by the consumer would cause the transaction to be rolled back.

When applied on the (child) bus level then all consumers (or handlers) will inherit that option.

`SqlTransaction`-s are created off the associated `SqlConnection`.

## How it works

- Outbox plugin uses the [interceptor](intro.md#interceptors) interface to tap into the relevant stages of message processing.

- Upon bus start the `Outbox` SQL table is created (if does not exist). The name of the table can be adjusted via settings.

- When a message is sent via a bus or producer maked with `.UseOutbox()` then such message will be inserted into the `Outbox` table.
It is important that message publish happens in the context of an transaction to ensure consistency.

- When the message publication happens in the context of a consumer (or handler) of another message, the `.UseTransactionScope()`, `.UseSqlTransaction()` can be used to start a transaction.

- The transaction can be managed by the application, starting it either explicitly using `DbContext.Database.BeginTransactionAsync()` or creating a `TransactionScope()`.

- The plugin accounts for distributed service running in multiple instances (concurrently).

- Message added to the `Outbox` table are initially owned by the respective service instance that created it, the message has a lock that expires at some point in time (driven by settings). Every service instance task attempts to publish their owned messages which happens in order of creaton (this ensures order of delivery within the same process).

- If a service instance where to crash or restart, the undelivered messages will be picked and locked by another instance.

- Once a message is picked from outbox and succesfully delivered then it is marked as sent in the outbox table.

- At configured intervals and after a certain time span the sent messages are removed from the outbox table.
