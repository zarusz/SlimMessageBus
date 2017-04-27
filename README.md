# SlimMessageBus

SlimMessageBus is a client façade for message brokers for .NET. It comes with implementations for specific brokers (Apache Kafka, Azure EventHub, MQTT/Mosquitto, Redis Pub/Sub) and also for in memory message passing (in-process communication). SlimMessageBus additionally provides request-response messaging implementation.

[![Build status](https://ci.appveyor.com/api/projects/status/6ppr19du717spq3s/branch/develop?svg=true)](https://ci.appveyor.com/project/zarusz/slimmessagebus/branch/develop)

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
  * `IConsumer<in TMessage>` - subscriber in pub/sub or queue consumer
  * `IRequestHandler<in TRequest, TResponse>` - request handler in req/resp
* Producers:
  * `IPublishBus` - publisher in pub/sub or queue producer
  * `IRequestResponseBus` - sender in req/resp
  * `IMessageBus`
* Misc
  * `IRequestMessage<TResponse>` - marker for request messages
  * `MessageBus`

## Principles
* The core of `SlimMessageBus` is "slim"
  * Simple, common and friendly API to work with messaging systems.
  * No external dependencies. Logging is done via `Common.Logging`, so that you can connect your favorite logger provider.
* Selectively add features you really need (e.g. Autofac integration, JSON serialization, your favorite messaging broker).
* Fluent configuration.
* No threads created (pure TPL and async).

## Packages

 Name | Descripton | Dependencies | NuGet
 ------------ | ------------- | ------------- | -------------
 `SlimMessageBus` | The interfaces to work with SlimMessageBus | `Common.Logging` | [NuGet](https://www.nuget.org/packages/SlimMessageBus)
 `SlimMessageBus.Host` | The common implementation for the hosting application layer | `SlimMessageBus` | [NuGet](https://www.nuget.org/packages/SlimMessageBus.Host)
 `SlimMessageBus.Host.Kafka` | Implementation for Apache Kafka  | `SlimMessageBus.Host` [`Confluent.Kafka`](https://www.nuget.org/packages/Confluent.Kafka/) | [NuGet](https://www.nuget.org/packages/SlimMessageBus.Host.Kafka)
 `SlimMessageBus.Host.EventHub` (future) | Implementation for Azure EventHub | `SlimMessageBus.Host` `Microsoft.Azure.ServiceBus.EventProcessorHost` | .
 `SlimMessageBus.Host.Redis` (future) | Implementation for Redis | `SlimMessageBus.Host` `StackExchange.Redis.StrongName` | .
 `SlimMessageBus.Host.InMemory` (pending) | Implementation for in memory broker (in-process message passing) | `SlimMessageBus.Host` | .
 `SlimMessageBus.Host.ServiceLocator` | Resolves dependencies from ServiceLocator | `SlimMessageBus.Host` `CommonServiceLocator` | [NuGet](https://www.nuget.org/packages/SlimMessageBus.Host.ServiceLocator)
 `SlimMessageBus.Host.Autofac` | Resolves dependencies from Autofac container | `SlimMessageBus.Host` `Autofac` | [NuGet](https://www.nuget.org/packages/SlimMessageBus.Host.Autofac)
 `SlimMessageBus.Host.Serialization.Json` | Message serialization provider for JSON | `SlimMessageBus.Host` `Newtonsoft.Json` | [NuGet](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.Json)

 Typically your application components only need to depend on `SlimMessageBus` which is the facade. Your application hosting layer (ASP.NET, Windows Service, Console App) will add and configure the other packages.

## Samples

Check out the [Samples](Samples/) folder.

### Usage examples

### Request-response with Kafka

Use case:
* The web app has to display thumbnails of large images.
* The thumbnails are requested in the WebApi and are generated on demand (and saved) by the Worker (unless they exist already).
* WebApi and Worker exchange messages via Apache Kafka

Frontend makes a call to resize an image 'DSC0862.jpg' to '120x80' in size, by using this URL:
```
http://localhost:50452/api/Image/DSC0862.jpg/?w=120&h=80&mode=1
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

Check out the full sample for image resizing application (`Sample.Images.WebApi` and `Sample.Images.Worker`)!

### Basic in-memory

This example is the simplest usage of `SlimMessageBus` and `SlimMessageBus.Host.InMemory` to implement Domain Events.

... Somewhere in your domain layer a domain event gets published

```cs
// Option 1
IMessageBus bus = ... // Injected from your favorite DI container
await bus.Publish(new NewUserJoinedEvent("Jane"));

// OR Option 2
await MessageBus.Current.Publish(new NewUserJoinedEvent("Jennifer"));
```

... The domain event is a simple POCO

```cs
public class NewUserJoinedEvent
{
	public string FullName { get; protected set; }

	public NewUserJoinedEvent(string fullName)
	{
		FullName = fullName;
	}
}
```

... The event handler implements the `IConsumer<T>` interface

```cs
public class NewUserHelloGreeter : IConsumer<NewUserJoinedEvent>
{
    public Task OnHandle(NewUserJoinedEvent message, string topic)
    {
        Console.WriteLine("Hello {0}", message.FullName);
    }
}
```

... The handler can be subscribed explicitly

```cs
// Get a hold of the handler
var greeter = new NewUserHelloGreeter();

// Register handler explicitly
bus.Subscribe(greeter);

// Events are being published here

bus.UnSubscribe(greeter);
```

... Or when you decide to use one of the container integrations  (e.g. `SlimMessageBus.Host.Autofac`) the handlers can be resolved from the DI container. For more details see other examples.

#### Setup

This is how you set the 'SlimMessageBus' up in the simplest use case.

```cs
// Define the recipie how to create our IMessageBus
var busBuilder = new MessageBusBuilder()
    .SimpleMessageBus();

// Create the IMessageBus instance from the builder
IMessageBus bus = busBuilder
    .Build();

// Set the provider to resolve our bus - this setup will work as a singleton.
MessageBus.SetProvider(() => bus);
```

## License

[Apache License 2.0](http://www.apache.org/licenses/LICENSE-2.0)

## Docs

* [Release Notes](docs/release_notes.md)
* [Kafka Notes](docs/kafka_notes.md)
