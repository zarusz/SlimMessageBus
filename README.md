# SlimMessageBus <!-- omit in toc -->

SlimMessageBus is a client façade for message brokers for .NET. It comes with implementations for specific brokers (RabbitMQ, Kafka, Azure EventHub, MQTT, Redis Pub/Sub) and in-memory message passing (in-process communication). SlimMessageBus additionally provides request-response implementation over message queues.

[![Gitter](https://badges.gitter.im/SlimMessageBus/community.svg)](https://gitter.im/SlimMessageBus/community?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge)
[![GitHub license](https://img.shields.io/github/license/zarusz/SlimMessageBus)](https://github.com/zarusz/SlimMessageBus/blob/master/LICENSE)
[![Build](https://github.com/zarusz/SlimMessageBus/actions/workflows/build.yml/badge.svg?branch=master)](https://github.com/zarusz/SlimMessageBus/actions/workflows/build.yml)

> The v2.0.0 major release is available.
> Please review the [release notes](https://github.com/zarusz/SlimMessageBus/releases/tag/Host.Transport-2.0.0).

- [Key elements of SlimMessageBus](#key-elements-of-slimmessagebus)
- [Docs](#docs)
- [Packages](#packages)
- [Samples](#samples)
  - [Basic usage](#basic-usage)
  - [Configuration](#configuration)
  - [Use Case: Domain Events (in-process pub/sub messaging)](#use-case-domain-events-in-process-pubsub-messaging)
  - [Use Case: MediatR replacement](#use-case-mediatr-replacement)
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
  - `IRequestHandler<in TRequest, TResponse>` & `IRequestHandler<in TRequest>` - request handler in request-response
- Producers:
  - `IPublishBus` - publisher in pub/sub (or queue producer)
  - `IRequestResponseBus` - sender in req/resp
  - `IMessageBus` - extends `IPublishBus` and `IRequestResponseBus`
- Misc:
  - `IRequest<out TResponse>` & `IRequest` - a marker for request messages
  - `MessageBus` - static accessor for current context `IMessageBus`

## Docs

- [Introduction](docs/intro.md)
- Providers:
  - [Azure EventHubs](docs/provider_azure_eventhubs.md)
  - [Azure ServiceBus](docs/provider_azure_servicebus.md)
  - [Apache Kafka](docs/provider_kafka.md)
  - [Hybrid](docs/provider_hybrid.md)
  - [Memory](docs/provider_memory.md)
  - [MQTT](docs/provider_mqtt.md)
  - [RabbitMQ](docs/provider_rabbitmq.md)
  - [Redis](docs/provider_redis.md)
- Plugins:
  - [Serialization](docs/serialization.md)
  - [Transactional Outbox](docs/plugin_outbox.md)
  - [Validation using FluentValidation](docs/plugin_fluent_validation.md)
  - [AsyncAPI specification generation](docs/plugin_asyncapi.md)

## Packages

| Name                                 | Description                                                                                                         | NuGet                                                                                                                                                                            |
| ------------------------------------ | ------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `SlimMessageBus`                     | The core API for SlimMessageBus                                                                                     | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.svg)](https://www.nuget.org/packages/SlimMessageBus)                                                                     |
| **Transport providers**              |                                                                                                                     |                                                                                                                                                                                  |
| `.Host.AzureEventHub`                | Transport provider for Azure Event Hubs                                                                             | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.AzureEventHub.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.AzureEventHub)                               |
| `.Host.AzureServiceBus`              | Transport provider for Azure Service Bus                                                                            | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.AzureServiceBus.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.AzureServiceBus)                           |
| `.Host.Kafka`                        | Transport provider for Apache Kafka                                                                                 | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Kafka.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Kafka)                                               |
| `.Host.Memory`                       | Transport provider implementation for in-process (in memory) message passing (no messaging infrastructure required) | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Memory.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Memory)                                             |
| `.Host.MQTT`                         | Transport provider for MQTT                                                                                         | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.MQTT.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.MQTT)                                                 |
| `.Host.RabbitMQ`                     | Transport provider for RabbitMQ                                                                                     | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.RabbitMQ.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.RabbitMQ)                                         |
| `.Host.Redis`                        | Transport provider for Redis                                                                                        | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Redis.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Redis)                                               |
| **Serialization**                    |                                                                                                                     |                                                                                                                                                                                  |
| `.Host.Serialization.Json`           | Serialization plugin for JSON (Newtonsoft.Json library)                                                             | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Serialization.Json.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.Json)                     |
| `.Host.Serialization.SystemTextJson` | Serialization plugin for JSON (System.Text.Json library)                                                            | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Serialization.SystemTextJson.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.SystemTextJson) |
| `.Host.Serialization.Avro`           | Serialization plugin for Avro (Apache.Avro library)                                                                 | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Serialization.Avro.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.Avro)                     |
| `.Host.Serialization.Hybrid`         | Plugin that delegates serialization to other serializers based on message type                                      | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Serialization.Hybrid.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.Hybrid)                 |
| `.Host.Serialization.GoogleProtobuf` | Serialization plugin for Google Protobuf                                                                            | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Serialization.GoogleProtobuf.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.GoogleProtobuf) |
| **Plugins**                          |                                                                                                                     |                                                                                                                                                                                  |
| `.Host.AspNetCore`                   | Integration for ASP.NET Core                                                                                        | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.AspNetCore.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.AspNetCore)                                     |
| `.Host.Interceptor`                  | Core interface for interceptors                                                                                     | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Interceptor.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Interceptor)                                   |
| `.Host.FluentValidation`             | Validation for messages based on [FluentValidation](https://www.nuget.org/packages/FluentValidation)                | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.FluentValidation.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.FluentValidation)                         |
| `.Host.Outbox.Sql`                   | Transactional Outbox using SQL                                                                                      | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Outbox.Sql.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.Sql)                                     |
| `.Host.Outbox.DbContext`             | Transactional Outbox using EF DbContext                                                                             | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Outbox.DbContext.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.DbContext)                         |
| `.Host.AsyncApi`                     | [AsyncAPI](https://www.asyncapi.com/) specification generation via [Saunter](https://github.com/tehmantra/saunter)  | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.AsyncApi.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.AsyncApi)                                         |

Typically the application layers (domain model, business logic) only need to depend on `SlimMessageBus` which is the facade, and ultimately the application hosting layer (ASP.NET, Console App, Windows Service) will reference and configure the other packages (`SlimMessageBus.Host.*`) which are the messaging transport providers and additional plugins.

## Samples

### Basic usage

Some service (or domain layer) publishes a message:

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

The bus also supports request-response implemented via queues, topics or in-memory - depending on the chosen transport provider.
The sender side sends a request message:

```cs
var response = await bus.Send(new SomeRequest());
```

> Note: It is possible to configure the bus to timeout a request when the response does not arrive within the allotted time (see [here](docs/intro.md#produce-request-message)).

The receiving side handles the request and replies:

```cs
public class SomeRequestHandler : IRequestHandler<SomeRequest, SomeResponse>
{
   public async Task<SomeResponse> OnHandle(SomeRequest request)
   {
      // handle the request message and return a response
      return new SomeResponse { /* ... */ };
   }
}
```

The bus will ask the DI container to provide the consumer instances (`SomeMessageConsumer`, `SomeRequestHandler`).

There is also support for [one-way request-response](docs/intro.md#request-without-response).

### Configuration

The `Microsoft.Extensions.DependencyInjection` is used to compose the bus:

```cs
// IServiceCollection services;

services.AddSlimMessageBus(mbb =>
{
   mbb
      // First child bus - in this example Kafka transport
      .AddChildBus("Bus1", (builder) => 
      {
         builder
            .Produce<SomeMessage>(x => x.DefaultTopic("some-topic"))
            .Consume<SomeMessage>(x => x.Topic("some-topic")
               //.WithConsumer<SomeMessageConsumer>() // Optional: can be skipped as IConsumer<SomeMessage> will be resolved from DI
               //.KafkaGroup("some-kafka-consumer-group") // Kafka: Consumer Group
               //.SubscriptionName("some-azure-sb-topic-subscription") // Azure ServiceBus: Subscription Name
            );
            // ...
            // Use Kafka transport provider (requires SlimMessageBus.Host.Kafka package)
            .WithProviderKafka(cfg => { cfg.BrokerList = "localhost:9092"; }); // requires SlimMessageBus.Host.Kafka package
            // Use Azure Service Bus transport provider
            //.WithProviderServiceBus(cfg => { ... }) // requires SlimMessageBus.Host.AzureServiceBus package
            // Use Azure Azure Event Hub transport provider
            //.WithProviderEventHub(cfg => { ... }) // requires SlimMessageBus.Host.AzureEventHub package
            // Use Redis transport provider
            //.WithProviderRedis(cfg => { ... }) // requires SlimMessageBus.Host.Redis package
            // Use RabbitMQ transport provider
            //.WithProviderRabbitMQ(cfg => { ... }) // requires SlimMessageBus.Host.RabbitMQ package
            // Use in-memory transport provider
            //.WithProviderMemory(cfg => { ... }) // requires SlimMessageBus.Host.Memory package
      })
      
      // Add other bus transports (as child bus), if needed
      //.AddChildBus("Bus2", (builder) => {  })

      // Scan assembly for consumers, handlers, interceptors, and register into MSDI
      .AddServicesFromAssemblyContaining<SomeMessageConsumer>()
      //.AddServicesFromAssembly(Assembly.GetExecutingAssembly());

      // Add JSON serializer
      .AddJsonSerializer(); // requires SlimMessageBus.Host.Serialization.Json or SlimMessageBus.Host.Serialization.SystemTextJson package
});
```

The configuration can be [modularized](docs/intro.md#modularization-of-configuration).

### Use Case: Domain Events (in-process pub/sub messaging)

This example shows how `SlimMessageBus` and `SlimMessageBus.Host.Memory` can be used to implement the Domain Events pattern.
The provider passes messages in the same process (no external message broker is required).

The domain event is a simple POCO:

```cs
// domain event
public record OrderSubmittedEvent(Order Order, DateTime Timestamp);
```

The domain event handler implements the `IConsumer<T>` interface:

```cs
// domain event handler
public class OrderSubmittedHandler : IConsumer<OrderSubmittedEvent>
{
   public Task OnHandle(OrderSubmittedEvent e)
   {
      // ...
   }
}
```

The domain event handler (consumer) is obtained from the MSDI at the time of event publication.
The event publish enlists in the ongoing scope (web request scope, external message scope of the ongoing message).

In the domain model layer, the domain event gets raised:

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

      // Raise domain event
      return MessageBus.Current.Publish(new OrderSubmittedEvent(this));
   }
}
```

Sample logic executed by the client of the domain model:

```cs
var john = new Customer("John", "Whick");

var order = new Order(john);
order.Add("id_machine_gun", 2);
order.Add("id_grenade", 4);

await order.Submit(); // events fired here
```

Notice the static [`MessageBus.Current`](src/SlimMessageBus/MessageBus.cs) property is configured to resolve a scoped `IMessageBus` instance (web request-scoped or pick-up message scope from a currently processed message).

The `SlimMessageBus` configuration for the in-memory provider looks like this:

```cs
//IServiceCollection services;

// Cofigure the message bus
services.AddSlimMessageBus(mbb => 
{
   mbb.WithProviderMemory();
   // Find types that implement IConsumer<T> and IRequestHandler<T, R> and declare producers and consumers on the mbb
   mbb.AutoDeclareFrom(Assembly.GetExecutingAssembly());
   // Scan assembly for consumers, handlers, interceptors and configurators and register into MSDI
   mbb.AddServicesFromAssemblyContaining<OrderSubmittedHandler>();
});
```

For the ASP.NET project, set up the `MessageBus.Current` helper (if you want to use it, and pick up the current web-request scope):

```cs
services.AddSlimMessageBus(mbb => 
{
   // ...
   mbb.AddAspNet(); // requires SlimMessageBus.Host.AspNetCore package
});
services.AddHttpContextAccessor(); // This is required by the SlimMessageBus.Host.AspNetCore plugin
```

See the complete [sample](/src/Samples#sampledomainevents) for ASP.NET Core where the handler and bus are web-request scoped.

### Use Case: MediatR replacement

The SlimMessageBus [in-memory provider](docs/provider_memory.md) can replace the need to use [MediatR](https://github.com/jbogard/MediatR) library:

- It has similar semantics and has the [interceptor pipeline](docs/intro.md#interceptors) enabling the addition of custom behavior.
- The [generic interceptors](docs/intro.md#generic-interceptors) can introduce common behavior like logging, authorization or audit of messages.
- The [FluentValidation plugin](docs/plugin_fluent_validation.md) can introduce request/command/query validation.
- The external communication can be layered on top of SlimMessageBus which allows having one library for in-memory and out-of-process messaging ([Hybrid Provider](docs/provider_hybrid.md)).

See the [CQRS and FluentValidation](/src/Samples/Sample.ValidatingWebApi/) samples.

### Use Case: Request-response over Kafka topics

See [sample](/src/Samples/README.md#sampleimages).

## Features

- Types of messaging patterns supported:
  - Publish-subscribe
  - Request-response
  - Queues
  - A hybrid of the above (e.g. Kafka with multiple topic consumers in one group)
- Modern async/await syntax and TPL
- Fluent configuration
- Because SlimMessageBus is a facade, you can swap broker implementations
  - Using NuGet pull another broker provider
  - Reconfigure SlimMessageBus and retest your app
  - Try out the messaging middleware that works best for your app (Kafka vs. Redis) without having to rewrite your app.

## Principles

- The core of `SlimMessageBus` is "slim"
  - Simple, common and friendly API to work with messaging systems
  - No external dependencies.
  - The core interface can be used in the domain model (e.g. Domain Events)
- Plugin architecture:
  - Message serialization (JSON, Avro, Protobuf)
  - Use your favorite messaging broker as a provider by simply pulling a NuGet package
  - Add transactional outbox pattern or message validation
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

NuGet packages end up in `dist` folder

## Testing

To run tests you need to update the respective `appsettings.json` to match your cloud infrastructure or local infrastructure.
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

Thanks to [Gravity9](https://www.gravity9.com/) for providing an Azure subscription that allows running the integration test infrastructure.

<a href="https://www.gravity9.com/" target="_blank"><img src="https://uploads-ssl.webflow.com/5ce7ef1205884e25c3d2daa4/5f71f56c89fd4db58dd214d3_Gravity9_logo.svg" width="100" alt="Gravity9"></a>

Thanks to the following service cloud providers for providing free instances for our integration tests:

- Redis - [Redis Labs](https://redislabs.com/)
- Kafka - [CloudKarafka](https://www.cloudkarafka.com/)
- MQTT - [HiveMQ](https://www.hivemq.com/)
- RabbitMQ - [CloudAMQP](https://www.cloudamqp.com/)

If you want to help and sponsor, please write to me.
