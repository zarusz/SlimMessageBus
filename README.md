# SlimMessageBus <!-- omit in toc -->

SlimMessageBus is a client façade for message brokers for .NET. It comes with implementations for specific brokers (Apache Kafka, Azure EventHub, MQTT/Mosquitto, Redis Pub/Sub) and in-memory message passing (in-process communication). SlimMessageBus additionally provides request-response implementation over message queues.

[![Gitter](https://badges.gitter.im/SlimMessageBus/community.svg)](https://gitter.im/SlimMessageBus/community?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge)
[![GitHub license](https://img.shields.io/github/license/zarusz/SlimMessageBus)](https://github.com/zarusz/SlimMessageBus/blob/master/LICENSE)
[![Build](https://github.com/zarusz/SlimMessageBus/actions/workflows/build.yml/badge.svg?branch=master)](https://github.com/zarusz/SlimMessageBus/actions/workflows/build.yml)

- [Key elements of SlimMessageBus](#key-elements-of-slimmessagebus)
- [Docs](#docs)
- [Packages](#packages)
- [Samples](#samples)
  - [Basic usage](#basic-usage)
  - [Configuration with MsDependencyInjection](#configuration-with-msdependencyinjection)
  - [Configuration with Autofac or Unity](#configuration-with-autofac-or-unity)
  - [Use Case: Domain Events (in-process pub/sub messaging)](#use-case-domain-events-in-process-pubsub-messaging)
  - [Use Case: Request-response over Kafka topics](#use-case-request-response-over-kafka-topics)
- [Features](#features)
- [Principles](#principles)
- [License](#license)
- [Build](#build)
- [Testing](#testing)
- [Credits](#credits)

## Key elements of SlimMessageBus

- Consumers:
  - `IConsumer<in TMessage>` - subscriber in pub/sub (or queue consumer)
  - `IRequestHandler<in TRequest, TResponse>` - request handler in request-response
- Producers:
  - `IPublishBus` - publisher in pub/sub (or queue producer)
  - `IRequestResponseBus` - sender in req/resp
  - `IMessageBus` - extends `IPublishBus` and `IRequestResponseBus`
- Misc:
  - `IRequestMessage<TResponse>` - marker for request messages
  - `MessageBus` - static accessor for current context `IMessageBus`

## Docs

- [Introduction](docs/intro.md)
- Providers:
  - [Apache Kafka](docs/provider_kafka.md)
  - [Azure ServiceBus](docs/provider_azure_servicebus.md)
  - [Azure EventHubs](docs/provider_azure_eventhubs.md)
  - [Redis](docs/provider_redis.md)
  - [Memory](docs/provider_memory.md)
  - [Hybrid](docs/provider_hybrid.md)
- [Serialization Plugins](docs/serialization.md)
- [Validation using FluentValidation](docs/plugin_fluent_validation.md)

## Packages

| Name                                 | Description                                                                                                         | NuGet                                                                                                                                                                                                                                                                                                                                                               |
| ------------------------------------ | ------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `SlimMessageBus`                     | The core API for SlimMessageBus                                                                                     | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.svg)](https://www.nuget.org/packages/SlimMessageBus)                                                                                                                                                                                                                                                        |
| **Transport providers**              |                                                                                                                     |                                                                                                                                                                                                                                                                                                                                                                     |
| `.Host.Kafka`                        | Transport provider for Apache Kafka                                                                                 | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Kafka.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Kafka)                                                                                                                                                                                                                                  |
| `.Host.AzureServiceBus`              | Transport provider for Azure Service Bus                                                                            | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.AzureServiceBus.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.AzureServiceBus)                                                                                                                                                                                                              |
| `.Host.AzureEventHub`                | Transport provider for Azure Event Hubs                                                                             | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.AzureEventHub.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.AzureEventHub)                                                                                                                                                                                                                  |
| `.Host.Redis`                        | Transport provider for Redis                                                                                        | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Redis.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Redis)                                                                                                                                                                                                                                  |
| `.Host.Memory`                       | Transport provider implementation for in-process (in memory) message passing (no messaging infrastructure required) | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Memory.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Memory)                                                                                                                                                                                                                                |
| `.Host.Hybrid`                       | Bus implementation that composes the bus out of other transport providers and performs message routing              | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Hybrid.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Hybrid)                                                                                                                                                                                                                                |
| **Serialization**                    |                                                                                                                     |                                                                                                                                                                                                                                                                                                                                                                     |
| `.Host.Serialization.Json`           | Serialization plugin for JSON (Newtonsoft.Json library)                                                             | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Serialization.Json.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.Json)                                                                                                                                                                                                        |
| `.Host.Serialization.SystemTextJson` | Serialization plugin for JSON (System.Text.Json library)                                                            | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Serialization.SystemTextJson.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.SystemTextJson)                                                                                                                                                                                    |
| `.Host.Serialization.Avro`           | Serialization plugin for Avro (Apache.Avro library)                                                                 | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Serialization.Avro.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.Avro)                                                                                                                                                                                                        |
| `.Host.Serialization.Hybrid`         | Plugin that delegates serialization to other serializers based on message type                                      | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Serialization.Hybrid.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.Hybrid)                                                                                                                                                                                                    |
| `.Host.Serialization.GoogleProtobuf` | Serialization plugin for Google Protobuf                                                                            | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Serialization.GoogleProtobuf.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.GoogleProtobuf)                                                                                                                                                                                    |
| **IoC Container**                    |                                                                                                                     |                                                                                                                                                                                                                                                                                                                                                                     |
| `.Host.MsDependencyInjection`        | DI adapter for Microsoft.Extensions.DependencyInjection                                                             | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.MsDependencyInjection.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.MsDependencyInjection)                                                                                                                                                                                                  |
| `.Host.AspNetCore`                   | Integration for ASP.NET Core (DI adapter, config helpers)                                                           | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.AspNetCore.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.AspNetCore)                                                                                                                                                                                                                        |
| `.Host.Autofac`                      | DI adapter for Autofac container                                                                                    | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Autofac.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Autofac)                                                                                                                                                                                                                              |
| `.Host.Unity`                        | DI adapter for Unity container                                                                                      | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Unity.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Unity)                                                                                                                                                                                                                                  |
| **Interceptors**                     |                                                                                                                     |                                                                                                                                                                                                                                                                                                                                                                     |
| `.Host.Interceptor`                  | Core interface for interceptors                                                                                     | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Interceptor.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Interceptor)                                                                                                                                                                                                                      |
| `.Host.FluentValidation`             | Validation for messages based on [FluentValidation](https://www.nuget.org/packages/FluentValidation)                | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.FluentValidation.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.FluentValidation) <br/> [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.FluentValidation.MsDependencyInjection.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.FluentValidation.MsDependencyInjection) |

Typically your application components (business logic, domain) only need to depend on `SlimMessageBus` which is the facade, and ultimately your application hosting layer (ASP.NET, Windows Service, Console App) will reference and configure the other packages (`SlimMessageBus.Host.*`) which are the providers and plugins.

## Samples

### Basic usage

Some service (or domain layer) sends a message:

```cs
IMessageBus bus; // injected

await bus.Publish(new SomeMessage());
```

Another service (or application layer) handles the message:

```cs
public class SomeMessageConsumer : IConsumer<SomeMessage>
{
   public async Task OnHandle(SomeMessage message)
   {
       // handle the message
   }
}
```

> Note: It is also possible to avoid having to implement the interface `IConsumer<T>` (see [here](docs/intro.md#consumer)).

The bus also supports request-response implemented via queues (or topics - depending on what the chosen transport provider supports). The sender side sends a request message:

```cs
var messageResponse = await bus.Send(new MessageRequest());
```

> Note: It is possible to configure the bus to timeout a request when the response does not arrive within the allotted time (see [here](docs/intro.md#produce-request-message)).

The receiving side handles the request and replies back:

```cs
public class MessageRequestHandler : IRequestHandler<MessageRequest, MessageResponse>
{
   public async Task<MessageResponse> OnHandle(MessageRequest request)
   {
      // handle the request message and return response
   }
}
```

The bus will ask the chosen DI to provide the consumer instances (`SomeMessageConsumer`, `MessageRequestHandler`).

### Configuration with MsDependencyInjection

When `Microsoft.Extensions.DependencyInjection` is used, then SMB can be configured (requires either [`MsDependencyInjection`](https://www.nuget.org/packages/SlimMessageBus.Host.MsDependencyInjection) or [`AspNetCore`](https://www.nuget.org/packages/SlimMessageBus.Host.AspNetCore) plugins):

```cs
IServiceCollection services;

services.AddSlimMessageBus(mbb =>
   {
      mbb
         .AddChildBus("Bus1", (builder) => 
         {
            builder
               .Produce<SomeMessage>(x => x.DefaultTopic("some-topic"))
               .Consume<SomeMessage>(x => x
                  .Topic("some-topic")
                  .WithConsumer<SomeMessageConsumer>()
                  //.KafkaGroup("some-kafka-consumer-group") //  Kafka provider specific
                  //.SubscriptionName("some-azure-sb-topic-subscription") // Azure ServiceBus provider specific
               );
               // ...
               .WithProviderKafka(new KafkaMessageBusSettings("localhost:9092")); // requires SlimMessageBus.Host.Kafka package
               // Use Azure Service Bus transport provider
               //.WithProviderServiceBus(...)
               // Use Azure Azure Event Hub transport provider
               //.WithProviderEventHub(...)
               // Use Redis transport provider
               //.WithProviderRedis(...)
               // Use in-memory transport provider
               //.WithProviderMemory(...)
         })
         // Add other bus transports, if needed
         //.AddChildBus("Bus2", (builder) => {})
         .WithSerializer(new JsonMessageSerializer()) // requires SlimMessageBus.Host.Serialization.Json package
         .WithProviderHybrid(); // requires SlimMessageBus.Host.Hybrid package
   }, 
   // Option 1 (optional)
   addConsumersFromAssembly: new[] { Assembly.GetExecutingAssembly() }, // auto discover consumers and register into DI (see next section)
   addInterceptorsFromAssembly: new[] { Assembly.GetExecutingAssembly() }, // auto discover interceptors and register into DI (see next section)
   addConfiguratorsFromAssembly: new[] { Assembly.GetExecutingAssembly() } // auto discover modular configuration and register into DI (see next section)
);

// Option 2 (optional)
services.AddMessageBusConsumersFromAssembly(Assembly.GetExecutingAssembly());
services.AddMessageBusInterceptorsFromAssembly(Assembly.GetExecutingAssembly());
services.AddMessageBusConfiguratorsFromAssembly(Assembly.GetExecutingAssembly());
```

### Configuration with Autofac or Unity

See the [Dependency Resolver](docs/intro.md#dependency-resolver) for more information.

### Use Case: Domain Events (in-process pub/sub messaging)

This example shows how `SlimMessageBus` and `SlimMessageBus.Host.Memory` can be used to implement the Domain Events pattern. The provider passes messages in the same app domain process (no external message broker is required).

The domain event is a simple POCO:

```cs
// domain event
public record OrderSubmittedEvent(Order Order, DateTime Timestamp);
```

The event handler implements the `IConsumer<T>` interface:

```cs
// domain event handler
public class OrderSubmittedHandler : IConsumer<OrderSubmittedEvent>
{
   public Task OnHandle(OrderSubmittedEvent e)
   {
      Console.WriteLine("Customer {0} {1} just placed an order for:", e.Order.Customer.Firstname, e.Order.Customer.Lastname);
      foreach (var orderLine in e.Order.Lines)
      {
         Console.WriteLine("- {0}x {1}", orderLine.Quantity, orderLine.ProductId);
      }

      Console.WriteLine("Generating a shipping order...");
      return Task.Delay(1000);
   }
}
```

The domain event handler (consumer) is obtained from the dependency resolver at the time of event publication.
It can be scoped (per web request, per unit of work) as configured in your favorite DI container.

Somewhere in your domain layer, the domain event gets raised:

```cs
// aggregate root
public class Order
{
   public Customer Customer { get; }
   public OrderState State { get; private set; }

   private IList<OrderLine> lines = new List<OrderLine>();
   public IEnumerable<OrderLine> Lines => lines.AsEnumerable();

   public Order(Customer customer)
   {
      Customer = customer;
      State = OrderState.New;
   }

   public OrderLine Add(string productId, int quantity) { }

   public Task Submit()
   {
      State = OrderState.Submitted;

      return MessageBus.Current.Publish(new OrderSubmittedEvent(this)); // raise domain event
   }
}
```

Some sample logic executed in your domain:

```cs
var john = new Customer("John", "Whick");

var order = new Order(john);
order.Add("id_machine_gun", 2);
order.Add("id_grenade", 4);

await order.Submit(); // events fired here
```

Notice the static `MessageBus.Current` property might actually be configured to resolve a scoped `IMessageBus` instance (web request-scoped or pick up message scope from an external bus).

The `SlimMessageBus` configuration for in-memory provider looks like this:

```cs
IServiceCollection services; // for MsDependencyInjection or AspNetCore

// Cofigure the message bus
services.AddSlimMessageBus(mbb => 
   {
      mbb            
         .WithProviderMemory()
         .AutoDeclareFrom(Assembly.GetExecutingAssembly()); // Find types that implement IConsumer<T> and IRequestHandler<T, R> and declare producers and consumers for them
   },
   addConsumersFromAssembly: new[] { Assembly.GetExecutingAssembly() } // Auto discover consumers and register inside DI container
);
```

In `Startup.cs` for the ASP.NET project, set up the `MessageBus.Current` helper (if you want to use it):

```cs
public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
   // Set the MessageBus provider, so that IMessageBus are resolved from the current request scope
   MessageBus.SetProvider(MessageBusCurrentProviderBuilder.Create().From(app).Build());
}
```

See the complete [sample](/src/Samples#sampledomainevents) for ASP.NET Core where the handler and bus are web-request scoped.

### Use Case: Request-response over Kafka topics

See [sample](/src/Samples/README.md#sampleimages).

## Features

- Types of messaging patterns supported:
  - Publish-subscribe
  - Request-response
  - Queues
  - Hybrid of the above (e.g. Kafka with multiple topic consumers in one group)
- Modern async/await syntax and TPL
- Fluent configuration
- Because SlimMessageBus is a facade, you have the ability to swap broker implementations
  - Using NuGet pull another broker provider
  - Reconfigure SlimMessageBus and retest your app
  - Try out the messaging middleware that works best for your app (Kafka vs. Redis) without having to rewrite your app.

## Principles

- The core of `SlimMessageBus` is "slim"
  - Simple, common and friendly API to work with messaging systems
  - No external dependencies.
  - The core interface can be used in domain model (e.g. Domain Events)
- Plugin architecture:
  - DI integration (Microsoft.Extensions.DependencyInjection, Autofac, CommonServiceLocator, Unity)
  - Message serialization (JSON, XML)
  - Use your favorite messaging broker as a provider by simply pulling a NuGet package
- No threads created (pure TPL)
- Async/Await support
- Fluent configuration
- Logging is done via [`Microsoft.Extensions.Logging.Abstractions`](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions/) so that you can connect to your favorite logger provider.

## License

[Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0)

## Build

```cmd
cd src
dotnet build
dotnet pack --output ../dist
```

NuGet packaged end up in `dist` folder

## Testing

To run tests you need to update the respective `appsettings.json` to match your own cloud infrastructure or local infrastructure.
SMB has some message brokers set up on Azure for integration tests (secrets not shared).

Run all tests:

```cmd
dotnet test
```

Run all tests except  integration tests that require local/cloud infrastructure:

```cmd
dotnet test --filter Category!=Integration
```

## Credits

Thanks to [Gravity9](https://www.gravity9.com/) for providing an Azure subscription that allows to run our integration test infrastructure.

<a href="https://www.gravity9.com/" target="_blank"><img src="https://uploads-ssl.webflow.com/5ce7ef1205884e25c3d2daa4/5f71f56c89fd4db58dd214d3_Gravity9_logo.svg" width="100" alt="Gravity9"></a>

Thanks to the following service cloud providers for providing free instances for our integration tests:

- Redis https://redislabs.com/
- Kafka https://www.cloudkarafka.com/

If you want to help and sponsor, please write to me.
