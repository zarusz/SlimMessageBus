
# Introduction for SlimMessageBus

## Configuration

The configuration starts with `MessageBusBuilder`, which allows us to configure couple of elements:
* The bus provider (Apache Kafka, Azure Service Bus, ...)
* The serialization plugin
* Declaration of messages produced and consumed along with topic/queue names
* Request-response configuration (if enabled)
* Provider specific settings

Here is a sample:

```cs
var mbb = MessageBusBuilder
	.Create()
	// Use JSON for message serialization                
	.WithSerializer(new JsonMessageSerializer())
	// Use DI from ASP.NET Core
	.WithDependencyResolver(new AspNetCoreMessageBusDependencyResolver(serviceProvider))
	// Use the Kafka Provider
	.WithProviderKafka(new KafkaMessageBusSettings("localhost:9092"));

	// Pub/Sub example:
	.Produce<AddCommand>(x => x.DefaultTopic(topicForAddCommand)) // By default AddCommand messages will go to event hub named 'add-command' (or topic if Kafka is chosen)
	.SubscribeTo<AddCommand>(x => x
		.Topic(topicForAddCommand)
		.Group(consumerGroup) // Kafka Provider specific
		.WithSubscriber<AddCommandConsumer>())

	// Req/Resp example:	
	.Produce<MultiplyRequest>(x => x.DefaultTopic(topicForMultiplyRequest)) // By default AddCommand messages will go to event hub named 'multiply-request' (or topic if Kafka is chosen)
	.Handle<MultiplyRequest, MultiplyResponse>(x => x
		.Topic(topicForMultiplyRequest) // topic to expect the request messages
		.Group(consumerGroup) // Kafka Provider specific
		.WithHandler<MultiplyRequestHandler>()
	)
	// Configure response message queue (on topic) when using req/resp
	.ExpectRequestResponses(x =>
	{
		x.ReplyToTopic(topicForResponses); // All responses from req/resp will return on this topic (the EventHub name)
		x.DefaultTimeout(TimeSpan.FromSeconds(20)); // Timeout request sender if response won't arrive within 10 seconds.
		x.Group(responseGroup); // Kafka Provider specific
	})
	
	.Do(builder =>
	{
		// do additional configuration logic wrapped in an block for convenience
	});
```

The builder is the blue print for creating message bus instances `IMessageBus`:

```cs
// Build the bus from the builder. 
// Message consumers will start consuming messages from the configured topics/queues of the chosen provider.
IMessageBus bus = mbb.Build();
```

In most scenarios having a singleton `IMessageBus` for your entire application will be sufficient. The provider implementations are thread-safe.

## Pub/Sub communication

Publish/subscribe communication is achieved using topics on pretty much all supported messaging system (Apache Kafka, Azure Service Bus, ...).

#### Producer

The app service that produces a given message needs to declare that on the `MessageBusBuilder` using `Produce<TMessage>()` method:

```cs
mbb.Produce<SomeMessage>(x => 
{
	// this is optional
	x.DefaultTopic("some-topic");
})
```

Then in your app you can publish the message to a topic

```cs
var msg = new SomeMessage("ping");
await bus.Publish(msg, "some-topic");

// or rely on the default topic name
await bus.Publish(msg);
```

### Consumer

Consuming the message in your app requires such configuration:

```cs
mbb.SubscribeTo<SomeMessage>(x => x
   .Topic("some-topic")
   .Group("some-consumer-group") // Kafka provider specific setting
   .WithSubscriber<SomeConsumer>())
   .Instances(1)
```

The consumer needs to implement the `IConsumer<SomeMessage>` interface:

```cs
public class SomeConsumer : IConsumer<SomeMessage>
{
	public async Task OnHandle(SomeMessage msg, string topic)
	{
		// handle the msg
	}
}
```

The `SomeConsumer` needs to be registered in your DI container. The SMB runtime will ask the chosen DI to provide the desired number of consumer instances.

## Request-response communication

SMB provides implementation of request-response over topics or queues - depending what the underlying provider supports.
This allows to asynchronously await a response for a request message in your application.

Typically this simplifies your system and allows to achive a reactive architecture that can scale out.

The idea is that each of your micro-system instance that sends request messages needs to have its own queue (or topic) onto which other micro-services send the response once the request is processed.
Each request message is wrapped by SMB into a special envelope that contains:
* Request send datetime (UTC, in epoch).
* Request ID, so that the request sender can correlate the arriving responses.
* ReplyTo topic/queue name, so that the service handling the request knows the to send back the response to.
* The body of the request message (serialized using the chosen serialization plugin)

### Produce request message

One requirement is that request messages implement the interface `IRequestMessage<TResponse>`:

```cs
public class SomeRequest : IRequestMessage<SomeResponse>
{
}

public class SomeResponse 
{
}
```

The micro-service that will be sending the request messages needs to enable request-response and configure its topic for response messages to arrive on:

```cs
// Configure response message queue (or topic) when using req/resp
.ExpectRequestResponses(x =>
{
	x.ReplyToTopic("servicename-instance1"); // All responses from req/resp will return on this topic (the EventHub name)
	x.DefaultTimeout(TimeSpan.FromSeconds(20)); // Timeout request sender if response won't arrive within 10 seconds.
	x.Group("some-consumer-group"); // This is kafka provider specific setting
})
```

Then, you need to declare that the `SomeRequest` messages will be sent:

```cs
mbb.Produce<SomeRequest>(x => 
{
	// this is optional
	x.DefaultTopic("do-some-computation-topic");
})
```

Once things are configured you can send the request message to a topic (or queue) like so:

```cs
var req = new SomeRequest("ping");
var res = await bus.Send(req, "do-other-computation-topic");

// or rely on the default topic name
var res = await bus.Send(req);
```

### Consume the request message (the request handler)

The request handling micro-service needs to have a handler that implements `IRequestHandler<SomeRequest, SomeResponse>`:

```cs
public class SomeRequestHandler : IRequestHandler<SomeRequest, SomeResponse>
{
	public async Task<MultiplyResponse> OnHandle(MultiplyRequest request, string topic)
	{
		// handle the request	
		return new SomeResponse("ping");
	}
}
```

This handler needs to be registered in the DI container.

Then, we need to configure that request messages are listened for and will be handled:

```cs
mbb.Handle<SomeRequest, SomeResponse>(x => x
		.Topic("do-some-computation-topic") // topic to expect the requests
		.Group("some-consumer-group") // kafka provider specific
		.WithHandler<SomeRequestHandler>()
	)
```

### Static accessor

The static `MessageBus.Current` was introduced to obtain either the singleton `IMessageBus` or the current scoped instance (like the `IMessageBus` tied with the currently executing request).

This helps to lookup the `IMessageBus` instance in the domain model layer while doing Domain-Driven Design and specifically to implement domain events or to externalize infrastructure concerns if anything changes on the domain that would require communication with external systems.

See `DomainEvents` sample how to configure it per-request scope and how to use it for domain events.

### Dependency resolver

SMB uses dependency resolver to obtain instances of the declared consumers (`IConsumer`, `IHandler`).
There are few plugins availble that allow to integrate SMB with your favorite DI framework. Consult samples and the [Packages section](../#Packages).


### Provider specific functionality

Providers introduce more settings and some subtleties to the above documentation.
For example Apache Kafka requires `mbb.Group(string)` for consumers to declare the consumer group, Azure Service Bus uses `mbb.SubscriptionName(string)` to set subscription name of the consumer, while Memory provider does not use anything like it.

Providers:
* [Apache Kafka](provider_kafka.md)
* [Azure Service Bus](provider_azure_servicebus.md)
* [Azure Event Hubs](provider_azure_eventhubs.md)
* [Memory](provider_memory.md)