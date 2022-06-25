# SlimMessageBus <!-- omit in toc -->

SlimMessageBus is a client façade for message brokers for .NET. It comes with implementations for specific brokers (Apache Kafka, Azure EventHub, MQTT/Mosquitto, Redis Pub/Sub) and in-memory message passing (in-process communication). SlimMessageBus additionally provides request-response implementation over message queues.

[![Gitter](https://badges.gitter.im/SlimMessageBus/community.svg)](https://gitter.im/SlimMessageBus/community?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge)
[![GitHub license](https://img.shields.io/github/license/zarusz/SlimMessageBus)](https://github.com/zarusz/SlimMessageBus/blob/master/LICENSE)
[![Build status](https://ci.appveyor.com/api/projects/status/6ppr19du717spq3s/branch/master?svg=true&passingText=master%20OK&pendingText=master%20pending&failingText=master%20failL)](https://ci.appveyor.com/project/zarusz/slimmessagebus/branch/master)
[![Build status](https://ci.appveyor.com/api/projects/status/6ppr19du717spq3s?svg=true&passingText=feature%20OK&pendingText=feature%20pending&failingText=feature%20fail)](https://ci.appveyor.com/project/zarusz/slimmessagebus)

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

## Packages

| Name                                               | Description                                                                                                         | NuGet                                                                                                                                                                            |
| -------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `SlimMessageBus`                                   | The core API for SlimMessageBus                                                                                     | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.svg)](https://www.nuget.org/packages/SlimMessageBus)                                                                     |
| **Transport providers**                            |                                                                                                                     |                                                                                                                                                                                  |
| `SlimMessageBus.Host.Kafka`                        | Transport provider for Apache Kafka                                                                                 | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Kafka.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Kafka)                                               |
| `SlimMessageBus.Host.AzureServiceBus`              | Transport provider for Azure Service Bus                                                                            | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.AzureServiceBus.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.AzureServiceBus)                           |
| `SlimMessageBus.Host.AzureEventHub`                | Transport provider for Azure Event Hubs                                                                             | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.AzureEventHub.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.AzureEventHub)                               |
| `SlimMessageBus.Host.Redis`                        | Transport provider for Redis                                                                                        | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Redis.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Redis)                                               |
| `SlimMessageBus.Host.Memory`                       | Transport provider implementation for in-process (in memory) message passing (no messaging infrastructure required) | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Memory.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Memory)                                             |
| `SlimMessageBus.Host.Hybrid`                       | Bus implementation that composes the bus out of other transport providers and performs message routing              | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Hybrid.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Hybrid)                                             |
| **Serialization**                                  |                                                                                                                     |                                                                                                                                                                                  |
| `SlimMessageBus.Host.Serialization.Json`           | Serialization plugin for JSON (Json.NET library)                                                                    | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Serialization.Json.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.Json)                     |
| `SlimMessageBus.Host.Serialization.Avro`           | Serialization plugin for Avro (Apache.Avro library)                                                                 | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Serialization.Avro.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.Avro)                     |
| `SlimMessageBus.Host.Serialization.Hybrid`         | Plugin that delegates serialization to other serializers based on message type                                      | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Serialization.Hybrid.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.Hybrid)                 |
| `SlimMessageBus.Host.Serialization.GoogleProtobuf` | Serialization plugin for Google Protobuf                                                                            | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Serialization.GoogleProtobuf.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.GoogleProtobuf) |
| **IoC Container**                                  |                                                                                                                     |                                                                                                                                                                                  |
| `SlimMessageBus.Host.MsDependencyInjection`        | DI adapter for Microsoft.Extensions.DependencyInjection                                                             | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.MsDependencyInjection.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.MsDependencyInjection)               |
| `SlimMessageBus.Host.AspNetCore`                   | Integration for ASP.NET Core (DI adapter, config helpers)                                                           | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.AspNetCore.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.AspNetCore)                                     |
| `SlimMessageBus.Host.Autofac`                      | DI adapter for Autofac container                                                                                    | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Autofac.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Autofac)                                           |
| `SlimMessageBus.Host.Unity`                        | DI adapter for Unity container                                                                                      | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Unity.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Unity)                                               |
| **Interceptors**                                   |                                                                                                                     |                                                                                                                                                                                  |
| `SlimMessageBus.Host.Interceptor`                  | Core interface for interceptors                                                                                     | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Interceptor.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Interceptor)                                   |

Typically your application components (business logic, domain) only need to depend on `SlimMessageBus` which is the facade, and ultimately your application hosting layer (ASP.NET, Windows Service, Console App) will reference and configure the other packages (`SlimMessageBus.Host.*`) which are the providers and plugins.

## Samples

### Basic usage

Some service (or domain layer) sends a message:

```cs
IMessageBus bus; // injected

await bus.Publish(new SomeMessage())
```

Another service (or application layer) handles the message:

```cs
public class SomeMessageConsumer : IConsumer<SomeMessage>
{
   public async Task OnHandle(SomeMessage message, string path) // path = topic or queue name
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
   public async Task<MessageResponse> OnHandle(MessageRequest request, string path)
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

services.AddSlimMessageBus((mbb, svp) =>
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
   public Task OnHandle(OrderSubmittedEvent e, string path)
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

services.AddSlimMessageBus((mbb, svp) =>
{
   // Define how to cofigure Message Bus
   mbb
      .Produce<OrderSubmittedEvent>(x => x.DefaultTopic(x.MessageType.Name))
      .Consume<OrderSubmittedEvent>(x => x.Topic(x.MessageType.Name).WithConsumer<OrderSubmittedHandler>())
      .WithProviderMemory(new MemoryMessageBusSettings
      {
         // Suppress serialization and pass the same event instance to subscribers (domain events contain domain objects we do not want to be serialized, we also gain on speed)
         EnableMessageSerialization = false
      });
});
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

Use case:

- Some front-end web app needs to display downsized image (thumbnails) of large images to speed up the page load.
- The thumbnails are requested in the WebApi and are generated on demand (and cached to disk) by the Worker (unless they exist already).
- WebApi and Worker exchange messages via Apache Kafka
- Worker can be scaled out (more instances, more Kafka partitions)

The front-end web app makes a call to resize an image `DSC0862.jpg` to `120x80` resolution, by using this URL:

`https://localhost:56788/api/image/DSC3781.jpg/r/?w=120&h=80&mode=1`

This gets handled by the WebApi method of the `ImageController`

```cs
private readonly IRequestResponseBus _bus;
// ...
[Route("{fileId}")]
public async Task<HttpResponseMessage> GetImageThumbnail(string fileId, ThumbnailMode mode, int w, int h)
{
   var thumbFileContent = // ... try to load content for the desired thumbnail w/h/mode/fileId
   if (thumbFileContent == null)
   {
      // Task will await until response comes back (or timeout happens). The HTTP request will be queued and IIS processing thread released.
      var thumbGenResponse = await _bus.Send(new GenerateThumbnailRequest(fileId, mode, w, h));
      thumbFileContent = await _fileStore.GetFile(thumbGenResponse.FileId);
   }
   return ServeStream(thumbFileContent);
}
```

The `GenerateThumbnailRequest` request is handled by a handler in one of the pool of Worker console apps.

```cs
public class GenerateThumbnailRequestHandler : IRequestHandler<GenerateThumbnailRequest, GenerateThumbnailResponse>
{
   public Task<GenerateThumbnailResponse> OnHandle(GenerateThumbnailRequest request, string path)
   {
      // some processing
      return new GenerateThumbnailResponse { FileId = thumbnailFileId };
   }
}
```

The response gets replied to the originating WebApi instance and the `Task<GenerateThumbnailResponse>` resolves causing the queued HTTP request to serve the resized image thumbnail.

```cs
var thumbGenResponse = await _bus.Send(new GenerateThumbnailRequest(fileId, mode, w, h));
```

The message bus configuration for the WebApi:

```cs
services.AddSlimMessageBus((mbb, svp) =>
{
   // unique id across instances of this application (e.g. 1, 2, 3)
   var instanceId = Configuration["InstanceId"];
   var kafkaBrokers = Configuration["Kafka:Brokers"];

   var instanceGroup = $"webapi-{instanceId}";
   var instanceReplyTo = $"webapi-{instanceId}-response";

   mbb
      .Produce<GenerateThumbnailRequest>(x =>
      {
         // Default response timeout for this request type
         //x.DefaultTimeout(TimeSpan.FromSeconds(10));
         x.DefaultTopic("thumbnail-generation");
      })
      .ExpectRequestResponses(x =>
      {
         x.ReplyToTopic(instanceReplyTo);
         x.KafkaGroup(instanceGroup);
         // Default global response timeout
         x.DefaultTimeout(TimeSpan.FromSeconds(30));
      })
      .WithSerializer(new JsonMessageSerializer())
      .WithProviderKafka(new KafkaMessageBusSettings(kafkaBrokers));
   });
}

services.AddHttpContextAccessor(); // This is required for the SlimMessageBus.Host.AspNetCore plugin
```

The message bus configuration for the Worker:

```cs
// This sample uses Autofac
var builder = new ContainerBuilder();

builder.RegisterModule(new SlimMessageBusModule
{
   ConfigureBus = (mbb, ctx) =>
   {
      // unique id across instances of this application (e.g. 1, 2, 3)
      var instanceId = configuration["InstanceId"];
      var kafkaBrokers = configuration["Kafka:Brokers"];

      var instanceGroup = $"worker-{instanceId}";
      var sharedGroup = "workers";

      mbb
         .Handle<GenerateThumbnailRequest, GenerateThumbnailResponse>(s =>
         {
            s.Topic("thumbnail-generation", t =>
            {
               t.WithHandler<GenerateThumbnailRequestHandler>()
                  .KafkaGroup(sharedGroup)
                  .Instances(3);
            });
         })
         .WithSerializer(new JsonMessageSerializer())
         .WithProviderKafka(new KafkaMessageBusSettings(kafkaBrokers)
         {
            ConsumerConfig = (config) =>
            {
               config.StatisticsIntervalMs = 60000;
               config.AutoOffsetReset = Confluent.Kafka.AutoOffsetReset.Latest;
            }
         });
   }
});
```

Because topics are partitioned in Kafka, requests originating from WebApi instances will be distributed across all Worker instances. However, to fine tune this, message key providers should be configured (see Kafka provider wiki and samples).

Check out the complete [sample](/src/Samples#sampleimages) for image resizing.

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

Thanks to the following service cloud providers for providing free instances for our integration tests:

- Redis https://redislabs.com/
- Kafka https://www.cloudkarafka.com/

Other test instances are hosted in Azure and paid for by the project maintainer. If you want to help and sponsor, please write to me.
