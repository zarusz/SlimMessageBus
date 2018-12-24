# SlimMessageBus

SlimMessageBus is a client façade for message brokers for .NET. It comes with implementations for specific brokers (Apache Kafka, Azure EventHub, MQTT/Mosquitto, Redis Pub/Sub) and also for in memory message passing (in-process communication). SlimMessageBus additionally provides request-response implementation over message queues.

| Branch    | Build Status |
| ----------| --------------------------------|
| master    | [![Build status](https://ci.appveyor.com/api/projects/status/6ppr19du717spq3s/branch/master?svg=true)](https://ci.appveyor.com/project/zarusz/slimmessagebus/branch/master) |
| develop   | [![Build status](https://ci.appveyor.com/api/projects/status/6ppr19du717spq3s/branch/develop?svg=true)](https://ci.appveyor.com/project/zarusz/slimmessagebus/branch/develop) |
| all       | [![Build status](https://ci.appveyor.com/api/projects/status/6ppr19du717spq3s?svg=true)](https://ci.appveyor.com/project/zarusz/slimmessagebus) |

### Features

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
  * `MessageBus` - singleton accessor for current `IMessageBus`

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

## Packages

| Name                                     | Descripton                                                          | NuGet                                                                          | .NET Standard |
|------------------------------------------|---------------------------------------------------------------------|--------------------------------------------------------------------------------|---------------|
| `SlimMessageBus`                         | The interfaces to work with SlimMessageBus                          | [NuGet](https://www.nuget.org/packages/SlimMessageBus)                         | 1.3           |
| `SlimMessageBus.Host.Kafka`              | Provider for Apache Kafka                                           | [NuGet](https://www.nuget.org/packages/SlimMessageBus.Host.Kafka)              | 1.3           |
| `SlimMessageBus.Host.AzureEventHub`      | Provider for Azure Event Hub                                        | [NuGet](https://www.nuget.org/packages/SlimMessageBus.Host.AzureEventHub)      | 2.0           |
| `SlimMessageBus.Host.Redis` (future)     | Provider for Redis                                                  | .                                                                              | .             |
| `SlimMessageBus.Host.Memory`             | Implementation for in-process (in memory) message passing           | [NuGet](https://www.nuget.org/packages/SlimMessageBus.Host.Memory)             | 1.3           |
| `SlimMessageBus.Host.ServiceLocator`     | DI adapter for ServiceLocator                                       | [NuGet](https://www.nuget.org/packages/SlimMessageBus.Host.ServiceLocator)     | 1.3           |
| `SlimMessageBus.Host.Autofac`            | DI adapter for Autofac container                                    | [NuGet](https://www.nuget.org/packages/SlimMessageBus.Host.Autofac)            | 1.3           |
| `SlimMessageBus.Host.Unity`              | DI adapter for Unity container                                      | [NuGet](https://www.nuget.org/packages/SlimMessageBus.Host.Unity)              | 1.3           |
| `SlimMessageBus.Host.AspNetCore`         | Integration for ASP.NET Core 2.1 (DI adapter, config helpers)       | [NuGet](https://www.nuget.org/packages/SlimMessageBus.Host.AspNetCore)         | 1.3           |
| `SlimMessageBus.Host.Serialization.Json` | Message serialization adapter for JSON (Json.NET)                   | [NuGet](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.Json) | 1.3           |

Typically your application components only need to depend on `SlimMessageBus` which is the facade. However, your application hosting layer (ASP.NET, Windows Service, Console App) will reference and configure the other packages (`SlimMessageBus.Host.*`) which are the providers and plugins.

## Samples

Check out the [Samples](src/Samples/) folder.

### Usage examples

### Request-response with Kafka

Use case:
* The web app has to display thumbnails of large images.
* The thumbnails are requested in the WebApi and are generated on demand (and saved) by the Worker (unless they exist already).
* WebApi and Worker exchange messages via Apache Kafka

Frontend makes a call to resize an image 'DSC0862.jpg' to '120x80' in size, by using this URL:
```
http://localhost:50452/api/Image/r/DSC0862.jpg/?w=120&h=80&mode=1
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

Check out the complete [sample](/src/Samples#sampleimages) for image resizing.

### Basic in-process messaging (for domain events)

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

The 'SlimMessageBus' configuration could looks like this:

```cs
// Define the recipie how to create our IMessageBus
var busBuilder = MessageBusBuilder
	.Publish<OrderSubmittedEvent>(x => x.DefaultTopic(x.MessageType.Name))
	.SubscribeTo<OrderSubmittedEvent>(x => x.Topic(x.MessageType.Name).WithSubscriber<OrderSubmittedHandler>())
	.WithDependencyResolverAsAutofac()
	.WithSerializer(new JsonMessageSerializer()) // Use JSON for message serialization                
	.WithProviderMemory(new MemoryMessageBusSettings
	{
		EnableMessageSerialization = false
	});

// Create the IMessageBus instance from the builder
IMessageBus bus = busBuilder
    .Build();

// Set the provider to resolve our bus - this setup will work as a singleton.
MessageBus.SetProvider(() => bus);
```

Examine the complete [sample](/src/Samples#sampledomainevents) for ASP.NET Core where the handler and bus is web-request scoped.

## License

[Apache License 2.0](http://www.apache.org/licenses/LICENSE-2.0)


## Docs

* [Release Notes](docs/release_notes.md)
* [Apache Kafka Wiki](docs/provider_kafka_notes.md)
* [Azure EventHubs Wiki](docs/provider_eventhubs_notes.md)

## Building

Build:
```
.\build\do_build.ps1
```

Test:
```
.\build\do_test.ps1
```

Create NuGet packages (artifacts go to `dist` folder):
```
.\build\do_package.ps1
```

Push NuGet packages to `local` repository:
```
.\build\do_push_local.ps1
```
