# Use Case: Migrating from MassTransit to SlimMessageBus with Azure Service Bus

This guide provides a practical example demonstrating the migration from using [MassTransit](https://masstransit.io/) with Azure Service Bus to [SlimMessageBus](https://github.com/zarusz/SlimMessageBus) with Azure Service Bus.

---

## Why migrate?

SlimMessageBus provides several advantages:

- Lightweight, fluent, and straightforward configuration.
- Native support for interceptors for logging, tracing, and validation.
- Easy extension to hybrid messaging scenarios.
- Consistent API across both in-memory and external message providers (e.g., Azure Service Bus).
- Free

---

## NuGet Packages

### MassTransit (before migration)

```shell
dotnet add package MassTransit
dotnet add package MassTransit.Azure.ServiceBus.Core
```

### SlimMessageBus (after migration)

```shell
dotnet add package SlimMessageBus
dotnet add package SlimMessageBus.Host.AzureServiceBus
dotnet add package SlimMessageBus.Host.Serialization.SystemTextJson
```

---

## MassTransit Example (`Program.cs`)

Here's how you typically configure MassTransit with Azure Service Bus and JSON serialization:

```csharp
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMassTransit(bus =>
{
    bus.UsingAzureServiceBus((context, cfg) =>
    {
        cfg.Host(builder.Configuration["AzureServiceBus:ConnectionString"]);

        cfg.ConfigureJsonSerializerOptions(opts =>
        {
            opts.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        });

        cfg.ReceiveEndpoint("order-queue", endpoint =>
        {
            endpoint.Consumer<OrderCreatedConsumer>();
        });
    });
});

var app = builder.Build();

app.MapGet("/", () => "MassTransit with Azure Service Bus running.");

app.Run();

// Message definition
public record OrderCreated(Guid OrderId);

// Consumer definition
public class OrderCreatedConsumer : IConsumer<OrderCreated>
{
    public async Task Consume(ConsumeContext<OrderCreated> context)
    {
        Console.WriteLine($"Order Created: {context.Message.OrderId}");
    }
}
```

---

## SlimMessageBus Example (`Program.cs`)

Here's an equivalent configuration using SlimMessageBus with Azure Service Bus:

```csharp
using SlimMessageBus;
using SlimMessageBus.Host.AzureServiceBus;
using SlimMessageBus.Host.Serialization.SystemTextJson;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSlimMessageBus(mbb =>
{
    mbb.WithProviderAzureServiceBus(cfg =>
    {
        cfg.ConnectionString = builder.Configuration["AzureServiceBus:ConnectionString"];
        cfg.SubscriptionName("my-service");
    });
    mbb.Produce<OrderCreated>(x => x.DefaultQueue("order-queue"));
    mbb.Consume<OrderCreated>(x => x.Queue("order-queue"));
    mbb.AddJsonSerializer();
    mbb.AddServicesFromAssemblyContaining<OrderCreatedEventConsumer>();
});

var app = builder.Build();

app.MapGet("/", () => "SlimMessageBus with Azure Service Bus running.");

app.Run();

// Message definition (same as before)
public record OrderCreated(Guid OrderId);

// Consumer definition adapted to SlimMessageBus
public class OrderCreatedConsumer : IConsumer<OrderCreated>
{
    public async Task OnHandle(OrderCreated message, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Order Created: {message.OrderId}");
    }
}
```

---

## Key Migration Points

- **Message Consumers**:

  - MassTransit uses `IConsumer<T>` interface with a `Consume()` method.
  - SlimMessageBus uses `IConsumer<T>` interface with an `OnHandle()` method.

- **Configuration Syntax**:

  - SlimMessageBus provides a fluent builder API for concise and intuitive configuration.

- **Simplicity and Flexibility**:
  - SlimMessageBus reduces complexity, yet offers advanced capabilities via interceptors and plugins.

---

## Additional Features in SlimMessageBus

- **Interceptors**: Easily add logging, tracing, or audit trails without altering message consumers.
- **Hybrid Providers**: Combine in-memory and external providers seamlessly, allowing versatile architectures.
- **Validation & Plugins**: Use built-in plugins like [FluentValidation](https://docs.fluentvalidation.net/) or Outbox.

---

For more advanced usage and additional features, please review the [SlimMessageBus documentation](/docs/).
