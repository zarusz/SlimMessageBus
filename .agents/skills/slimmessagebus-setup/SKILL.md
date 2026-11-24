---
name: slimmessagebus-setup
description: Set up SlimMessageBus in a .NET application. Use for first-time integration in ASP.NET Core, a worker, or a Generic Host console app, including package references, dependency injection, serialization, and a local smoke test.
---

# Set up SlimMessageBus

## Integrate with the application host

1. Inspect the startup project, its target framework, and any `Directory.Packages.props`. Match the project's versioning and dependency injection conventions. Keep existing SlimMessageBus versions that work, and verify new APIs against the corresponding package version or release source. Package versions can differ across the core, host, and plugins.
2. Reference `SlimMessageBus` where code uses its public contracts. Reference the selected `SlimMessageBus.Host.*` transport from the startup project. Keep transport settings and credentials in the hosting layer. For a broker, configure a serializer that matches the communicating applications. For a new JSON integration, use `SlimMessageBus.Host.Serialization.SystemTextJson` and `AddJsonSerializer()` from its namespace. Keep the existing serializer when extending an application.
3. Configure `builder.Services.AddSlimMessageBus(...)` before building the host. Extend the existing bus or module registration if present. Register consumers and handlers in DI and declare their endpoints on the bus. The Generic Host starts the bus through a hosted service.
4. For a broker-free, single-process example, use `SlimMessageBus.Host.Memory`. Its serialization is disabled by default. Use the requested transport for communication between processes.

## Run a local smoke test

This Generic Host console example uses the `Microsoft.Extensions.Hosting` and `SlimMessageBus.Host.Memory` packages. An existing ASP.NET Core or worker app can use the same registration in its startup code.

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SlimMessageBus;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSlimMessageBus(bus =>
{
    bus.WithProviderMemory();
    bus.Produce<OrderPlaced>(producer => producer.DefaultPath("orders"));
    bus.Consume<OrderPlaced>(consumer => consumer.Path("orders"));
    bus.AddServicesFromAssemblyContaining<OrderPlacedConsumer>();
});

using var host = builder.Build();
await host.StartAsync();
await using (var scope = host.Services.CreateAsyncScope())
{
    var bus = scope.ServiceProvider.GetRequiredService<IPublishBus>();
    await bus.Publish(new OrderPlaced(42));
}
await host.StopAsync();

public record OrderPlaced(int OrderId);

public sealed class OrderPlacedConsumer : IConsumer<OrderPlaced>
{
    public Task OnHandle(OrderPlaced message, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Received order {message.OrderId}");
        return Task.CompletedTask;
    }
}
```

Build the affected project and, when the environment permits, start the host and verify that the consumer receives the message. For a worker or singleton hosted service, resolve the bus from the DI scope used for the operation if it needs scoped dependencies.

Use the documentation for your installed release if it differs from the current examples. [Introduction](https://github.com/zarusz/SlimMessageBus/blob/master/docs/intro.md) · [Memory provider](https://github.com/zarusz/SlimMessageBus/blob/master/docs/provider_memory.md)
