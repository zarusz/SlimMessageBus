# SlimMessageBus

SlimMessageBus is a client façade for message brokers for .NET. It comes with implementations for specific brokers (Apache Kafka, Azure EventHub, MQTT/Mosquitto, Redis Pub/Sub) and also for in memory message passing (in-process communication). SlimMessageBus additionally provides request-response implementation over message queues.

| Branch    | Build Status |
| ----------| --------------------------------|
| master    | [![Build status](https://ci.appveyor.com/api/projects/status/6ppr19du717spq3s/branch/master?svg=true)](https://ci.appveyor.com/project/zarusz/slimmessagebus/branch/master) |
| develop   | [![Build status](https://ci.appveyor.com/api/projects/status/6ppr19du717spq3s/branch/develop?svg=true)](https://ci.appveyor.com/project/zarusz/slimmessagebus/branch/develop) |
| all       | [![Build status](https://ci.appveyor.com/api/projects/status/6ppr19du717spq3s?svg=true)](https://ci.appveyor.com/project/zarusz/slimmessagebus) |

## Key elements of SlimMessageBus
* Consumers:
  * `IConsumer<in TMessage>` - subscriber in pub/sub (or queue consumer)
  * `IRequestHandler<in TRequest, TResponse>` - request handler in request-response
* Producers:
  * `IPublishBus` - publisher in pub/sub (or queue producer)
  * `IRequestResponseBus` - sender in req/resp
  * `IMessageBus` - extends `IPublishBus` and `IRequestResponseBus`
* Misc:
  * `IRequestMessage<TResponse>` - marker for request messages
  * `MessageBus` - static accessor for current context `IMessageBus`

## Docs

* [Introduction](docs/intro.md)
* Providers:
	* [Apache Kafka](docs/provider_kafka.md)
	* [Azure ServiceBus](docs/provider_azure_servicebus.md)
	* [Azure EventHubs](docs/provider_azure_eventhubs.md)
	* [Memory](docs/provider_memory.md)

## Packages

| Name                                            | Descripton                                                                                       | NuGet                                                                          | .NET Standard |
|-------------------------------------------------|--------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------|---------------|
| `SlimMessageBus`                                | The interfaces to work with SlimMessageBus                                                       | [NuGet](https://www.nuget.org/packages/SlimMessageBus)                         | 1.3           |
| `SlimMessageBus.Host.Kafka`                     | Provider for Apache Kafka                                                                        | [NuGet](https://www.nuget.org/packages/SlimMessageBus.Host.Kafka)              | 1.3           |
| `SlimMessageBus.Host.AzureEventHub`             | Provider for Azure Event Hub                                                                     | [NuGet](https://www.nuget.org/packages/SlimMessageBus.Host.AzureEventHub)      | 2.0           |
| `SlimMessageBus.Host.AzureServiceBus` (beta)    | Provider for Azure Service Bus                                                                   | [NuGet](https://www.nuget.org/packages/SlimMessageBus.Host.AzureServiceBus)    | 2.0           |
| `SlimMessageBus.Host.Redis` (future)            | Provider for Redis                                                                               | .                                                                              | .             |
| `SlimMessageBus.Host.Memory`                    | Implementation for in-process (in memory) message passing (no messaging infrastructure required) | [NuGet](https://www.nuget.org/packages/SlimMessageBus.Host.Memory)             | 1.3           |
| `SlimMessageBus.Host.Serialization.Json`        | Message serialization adapter for JSON (Json.NET)                                                | [NuGet](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.Json) | 1.3           |
| `SlimMessageBus.Host.AspNetCore`                | Integration for ASP.NET Core 2.x (DI adapter, config helpers)                                    | [NuGet](https://www.nuget.org/packages/SlimMessageBus.Host.AspNetCore)         | 1.3           |
| `SlimMessageBus.Host.ServiceLocator`            | DI adapter for ServiceLocator                                                                    | [NuGet](https://www.nuget.org/packages/SlimMessageBus.Host.ServiceLocator)     | 1.3           |
| `SlimMessageBus.Host.Autofac`                   | DI adapter for Autofac container                                                                 | [NuGet](https://www.nuget.org/packages/SlimMessageBus.Host.Autofac)            | 1.3           |
| `SlimMessageBus.Host.Unity`                     | DI adapter for Unity container                                                                   | [NuGet](https://www.nuget.org/packages/SlimMessageBus.Host.Unity)              | 1.3           |

Typically your application components (business logic, domain) only need to depend on `SlimMessageBus` which is the facade, and ultimately your application hosting layer (ASP.NET, Windows Service, Console App) will reference and configure the other packages (`SlimMessageBus.Host.*`) which are the providers and plugins.

## Samples

Check out the [Samples](src/Samples/) folder.

### Quick example

Some service sends a message:

```cs
IMessageBus bus; // injected

await bus.Publish(new SomeMessage())
```

Another service handles the message:

```cs
public class SomeMessageConsumer : IConsumer<SomeMessage>
{
	public async Task OnHandle(SomeMessage message, string topic)
	{
		// handle the message
	}
}
```

The configuration somewhere in your service:

```cs
var mbb = MessageBusBuilder
	.Create()
	
	// Use JSON for message serialization                
	.WithSerializer(new JsonMessageSerializer())
	// Use DI from ASP.NET Core (or Autofac, Unity, ServiceLocator)
	.WithDependencyResolver(new AspNetCoreMessageBusDependencyResolver(serviceProvider))
	
	// Use Apache Kafka transport provider
	.WithProviderKafka(new KafkaMessageBusSettings("localhost:9092"));
	// Use Azure Service Bus transport provider
	//.WithProviderServiceBus(...)
	// Use Azure Azure Event Hub transport provider
	//.WithProviderEventHub(...)
	// Use in-memory transport provider
	//.WithProviderMemory(...)

	// Pub/Sub example:
	.Produce<SomeMessage>(x => x.DefaultTopic("some-topic"))
	.Consume<SomeMessage>(x => x
		.Topic("some-topic")
		.WithConsumer<SomeMessageConsumer>()
		.Group("some-kafka-consumer-group") // Kafka provider specific
		//.SubscriptionName("some-azure-sb-topic-subscription") // Azure Service Bus provider specific
	);

// Build the bus from the builder. Message consumers will start consuming messages from the configured topics/queues of the chosen provider.
IMessageBus bus = mbb.Build();
```

The bus will ask the chosen DI to provide the consumer instances (`SomeMessageConsumer`).

### Basic in-process pub/sub messaging (for domain events)

This example shows how `SlimMessageBus` and `SlimMessageBus.Host.Memory` can be used to implement Domain Events pattern. The provider passes messages in the same app domain process (no external message broker is required).

The domain event is a simple POCO:

```cs
// domain event
public class OrderSubmittedEvent
{
	public Order Order { get; }
	public DateTime Timestamp { get; }

	public OrderSubmittedEvent(Order order)	{ ... }
}
```

The event handler implements the `IConsumer<T>` interface:

```cs
// domain event handler
public class OrderSubmittedHandler : IConsumer<OrderSubmittedEvent>
{
	public Task OnHandle(OrderSubmittedEvent e, string topic)
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
The domain handler (well, really the consumer) is obtained from dependency resolver at the time of event publication. It can be scoped (per web request, per unit of work) as configured in your favorite DI container.

Somewhere in your domain layer the domain event gets raised:

```cs
// aggregate root
public class Order
{
	public Customer Customer { get; }
	private IList<OrderLine> _lines = new List<OrderLine>();
	public OrderState State { get; private set; }

	public IEnumerable<OrderLine> Lines => _lines.AsEnumerable();

	public Order(Customer customer)
	{
		State = OrderState.New;
		Customer = customer;
	}

	public OrderLine Add(string productId, int quantity) { }

	public void Submit()
	{
		State = OrderState.Submitted;

		var e = new OrderSubmittedEvent(this);
		MessageBus.Current.Publish(e).Wait(); // raise domain event
	}
}
```

Some sample logic executed in your domain:

```cs
var john = new Customer("John", "Whick");

var order = new Order(john);
order.Add("id_machine_gun", 2);
order.Add("id_grenade", 4);

order.Submit(); // events fired here
```

Notice the static `MessageBus.Current` property might actually be configured to resolve a scoped `IMessageBus` instance (web request scoped).

The `SlimMessageBus` configuration for in-memory provider looks like this:

```cs
// Define the recipie how to create our IMessageBus
var mbb = MessageBusBuilder
	.Create()
	.Produce<OrderSubmittedEvent>(x => x.DefaultTopic(x.MessageType.Name))
	.Consume<OrderSubmittedEvent>(x => x.Topic(x.MessageType.Name).WithConsumer<OrderSubmittedHandler>())
	.WithDependencyResolver(new AutofacMessageBusDependencyResolver())
	.WithProviderMemory(new MemoryMessageBusSettings
	{
		// supress serialization and pass the same event instance to subscribers (events contain domain objects we do not want serialized, also we gain abit on speed)
		EnableMessageSerialization = false
	});

// Create the IMessageBus instance from the builder
IMessageBus bus = mbb.Build();

// Set the provider to resolve our bus - this setup will work as a singleton.
MessageBus.SetProvider(() => bus);
```

See the complete [sample](/src/Samples#sampledomainevents) for ASP.NET Core where the handler and bus is web-request scoped.

### Request-response over Kafka topics

Use case:
* Some front-end web app needs to display downsized image (thumbnails) of large images to speed up page load.
* The thumbnails are requested in the WebApi and are generated on demand (and cached to disk) by the Worker (unless they exist already).
* WebApi and Worker exchange messages via Apache Kafka
* Worker can be scaled out (more instances, more kafka partitions)

Front-end web app makes a call to resize an image `DSC0862.jpg` to `120x80` resolution, by using this URL:
```
http://localhost:56788/api/image/DSC3781.jpg/r/?w=120&h=80&mode=1
```

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
   public async Task<GenerateThumbnailResponse> OnHandle(GenerateThumbnailRequest request, string topic)
   {
     // some processing
     return new GenerateThumbnailResponse
     {
         FileId = thumbnailFileId
     };
   }
}
```

The response gets replied onto the originating WebApi instance and the Task<GenerateThumbnailResponse> resolves causing the queued HTTP request to serve the resized image thumbnail.
```cs
var thumbGenResponse = await _bus.Send(new GenerateThumbnailRequest(fileId, mode, w, h));
```

The message bus configuration for the WebApi:
```cs
private IMessageBus BuildMessageBus()
{
    // unique id across instances of this application (e.g. 1, 2, 3)
    var instanceId = Configuration["InstanceId"];
    var kafkaBrokers = Configuration["Kafka:Brokers"];

    var instanceGroup = $"webapi-{instanceId}";
    var instanceReplyTo = $"webapi-{instanceId}-response";

    var mbb = MessageBusBuilder
	.Create()
	.Produce<GenerateThumbnailRequest>(x =>
	{
	    //x.DefaultTimeout(TimeSpan.FromSeconds(10)); // Default response timeout for this request type
	    x.DefaultTopic("thumbnail-generation"); // Use this topic as default when topic is not specified in IMessageBus.Publish() for that message type
	})
	.ExpectRequestResponses(x =>
	{
	    x.ReplyToTopic(instanceReplyTo); // Expect all responses to my reqests replied to this topic
	    x.Group(instanceGroup); // Kafka consumer group	    
	    x.DefaultTimeout(TimeSpan.FromSeconds(30)); // Default global response timeout
	})
	.WithDependencyResolver(new AutofacMessageBusDependencyResolver())
	.WithSerializer(new JsonMessageSerializer())
	.WithProviderKafka(new KafkaMessageBusSettings(kafkaBrokers));

    var messageBus = mbb.Build();
    return messageBus;
}
```

The message bus configuration for the Worker:

```cs
private static IMessageBus BuildMessageBus()
{
    // unique id across instances of this application (e.g. 1, 2, 3)
    var instanceId = Configuration["InstanceId"];
    var kafkaBrokers = Configuration["Kafka:Brokers"];

    var instanceGroup = $"worker-{instanceId}";
    var sharedGroup = "workers";

    var mbb = MessageBusBuilder
	.Create()
	.Handle<GenerateThumbnailRequest, GenerateThumbnailResponse>(s =>
	{
		s.Topic("thumbnail-generation", t =>
		{
			t.WithHandler<GenerateThumbnailRequestHandler>()
				.Group(sharedGroup) // kafka consumer group
				.Instances(3);
		});
	})
	.WithDependencyResolver(new AutofacMessageBusDependencyResolver())
	.WithSerializer(new JsonMessageSerializer())
	.WithProviderKafka(new KafkaMessageBusSettings(kafkaBrokers));

    var messageBus = mbb.Build();
    return messageBus;
}
```

Because topics are partitioned in Kafka, requests originating from WebApi instances will be distributed across all Worker instances. However, to fine tune this, message key providers should configured (see Kafka provider wiki and samples).

Check out the complete [sample](/src/Samples#sampleimages) for image resizing.

## Features

* Types of messaging patterns supported:
  * Publish-subscribe
  * Request-response
  * Queues
  * Hybrid of the above (e.g. Kafka with multiple topic consumers in one group)
* Modern async/await syntax and TPL
* Fluent configuration
* Because SlimMessageBus is a facade, you have the ability to swap broker implementations
  * Using nuget pull another broker provider
  * Reconfigure SlimMessageBus and retest your app
  * Try out the messaging middleware that works best for your app (Kafka vs. Redis) without having to rewrite your app.

## Principles
* The core of `SlimMessageBus` is "slim"
  * Simple, common and friendly API to work with messaging systems
  * No external dependencies. Logging is done via `Common.Logging`, so that you can connect your favorite logger provider.
  * The core interface can be used in domain model (e.g. DomainEvents)
* Plugin architecture:
  * DI integration (Autofac, CommonServiceLocator, Unity)
  * Message serialization (JSON, XML)
  * Use your favorite messaging broker as provider by simply pulling a nuget package
* No threads created (pure TPL)
* Async/Await support
* Fluent configuration


## License

[Apache License 2.0](http://www.apache.org/licenses/LICENSE-2.0)

## Build

The PowerShell scripts use the `dotnet` CLI.

| What                                             | Command                     | Comment                                                                                                                                                                                                   |
|--------------------------------------------------|-----------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Build                                            | `.\build\do_build.ps1`      |                                                                                                                                                                                                           |
| Test                                             | `.\build\do_test.ps1`       | Please note there are integration tests that require local infrastructure (Kafka provider), other use shared clound infrastructre (Azure EventHubs). Consult each provider test to see what is required.  |
| Test (skip tests requiring local infrastructure) | `.\build\do_test_ci.ps1`    | Executs tests that do not require local infrastructure.                                                                                                                                                   |
| Package NuGet                                    | `.\build\do_package.ps1`    | Packages go to `dist` folder.                                                                                                                                                                             |
| Push NuGet                                       | `.\build\do_push_local.ps1` | Pushes packages to nuget repository named `local`.                                                                                                                                                        |
