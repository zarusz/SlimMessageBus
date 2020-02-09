
# Introduction for SlimMessageBus

## Configuration

The configuration starts with `MessageBusBuilder`, which allows to configure couple of elements:

* The bus transport provider (Apache Kafka, Azure Service Bus, Memory).
* The serialization provider.
* The dependency injecion provider.
* Declaration of messages produced and consumed along with topic/queue names.
* Request-response configuration (if enabled).
* Additional provider specific settings (message partition key, message id, etc).

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
  .Produce<AddCommand>(x => x.DefaultTopic("add-command")) // By default AddCommand messages will go to 'add-command' topic (or hub name when Azure Service Hub provider)
  .Consume<AddCommand>(x => x
    .Topic("add-command")
    .WithConsumer<AddCommandConsumer>()
    .Group(consumerGroup) // Kafka provider specific
  )

  // Req/Resp example:
  .Produce<MultiplyRequest>(x => x.DefaultTopic("multiply-request")) // By default AddCommand messages will go to 'multiply-request' topic (or hub name when Azure Service Hub provider)
  .Handle<MultiplyRequest, MultiplyResponse>(x => x
    .Topic("multiply-request") // Topic to expect the request messages
    .WithHandler<MultiplyRequestHandler>()
    .Group(consumerGroup) // Kafka provider specific
  )
  // Configure response message queue (on topic) when using req/resp
  .ExpectRequestResponses(x =>
  {
    x.ReplyToTopic(topicForResponses); // All responses from req/resp will return on this topic (the EventHub name)
    x.DefaultTimeout(TimeSpan.FromSeconds(20)); // Timeout request sender if response won't arrive within 10 seconds.
    x.Group(responseGroup); // Kafka provider specific
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

The `IMessageBus` is disposable (implements `IDisposable`).

## Pub/Sub communication

### Producer

The app service that produces a given message needs to declare that on the `MessageBusBuilder` using `Produce<TMessage>()` method:

```cs
mbb.Produce<SomeMessage>(x =>
{
  // this is optional
  x.DefaultTopic("some-topic");
  //x.WithModifier(...) other provider specific extensions
})
```

Then your app can publish a message:

```cs
var msg = new SomeMessage("ping");

// delivered to "some-topic" by default
await bus.Publish(msg);

// OR delivered to the specified topic (or queue)
await bus.Publish(msg, "other-topic");
```

> The transport plugins might introduce additional configuration option. Please check the relevant provider docs. For example, Azure Service Bus and Kafka allows to set the portitioning key for a message type.

#### Polymorphic messages

Consider the following message types that can be produced:

```cs
public class CustomerEvent
{
  public DateTime Created { get; set; }
  public Guid CustomerId { get; set; }
}

public class CustomerCreatedEvent : CustomerEvent { }
public class CustomerChangedEvent : CustomerEvent { }
```

If we want the bus to deliver all 3 messages types into the same topic (or queue), we can configure just the base type:

```cs
// Will apply to CustomerCreatedEvent and CustomerChangedEvent
mbb.Produce<CustomerEvent>(x => x.DefaultTopic("customer-events"));
```

Then all of the those message types will follow the base message type producer configuration. 
In this example, all messages will be delivered to topic `customer-events`:

```cs
await bus.Publish(new CustomerEvent { });
await bus.Publish(new CustomerCreatedEvent { });
await bus.Publish(new CustomerChangedEvent { });
```

> Sending messages of different types into the same topic (or queue) makes sense if the underlying serializer (e.g. Newtonsoft.Json) supports polimorphic serialization. In such case a message type discriminator (e.g. `$type` property for Newtonsoft.Json) will be added by the serializer, so that the consumer end knows to what derived type to deserialize the message to.

### Consumer

To consume a message type from a topic/queue, declare it using `Consume<TMessage>()` method:

```cs
mbb.Consume<SomeMessage>(x => x
  .Topic("some-topic") // or queue name
  .WithConsumer<SomeConsumer>() // (1)
  // if you do not want to implement the IConsumer<T> interface
  // .WithConsumer<AddCommandConsumer>(nameof(AddCommandConsumer.MyHandleMethod)) // (2) uses reflection
  // .WithConsumer<AddCommandConsumer>((consumer, message, name) => consumer.MyHandleMethod(message, name)) // (3) uses a delegate
  .Instances(1)
  //.Group("some-consumer-group")) // Kafka provider specific extensions
```

When the consumer implements the `IConsumer<SomeMessage>` interface:

```cs
public class SomeConsumer : IConsumer<SomeMessage>
{
  public async Task OnHandle(SomeMessage msg, string name)
  {
    // handle the msg
  }
}
```

The second parameter (`name`) is the topic (or queue) name that the message arrived on.

The `SomeConsumer` needs to be registered in your DI container. The SMB runtime will ask the chosen DI to provide the desired number of consumer instances. Any collaborators of the consumer will be resolved according to your DI configuration.

Alternatively, if you do not want to implement the `IConsumer<SomeMessage>`, then you can provide the method name (2) or a delegate that calls the consumer method (3):

```cs
public class SomeConsumer
{
  public async Task MyHandleMethod(SomeMessage msg, string name)
  {
    // handle the msg
  }

  // This is also possible:
  // private async Task MyHandleMethod(SomeMessage msg)
  // {
  // }
}
```

## Request-response communication

SMB provides implementation of request-response over topics (or queues - depending what the underlying provider supports).
This allows to asynchronously await a response for a request message in your service.

Typically this simplifies service interactions that need to await for a result and still allows to achive a reactive architecture that can scale out.

The idea is that each of your micro-service instance that sends request messages needs to have its own queue (or topic) onto which other micro-services send the response once the request is processed.
Each request message is wrapped by SMB into a special envelope that is messaging provider agnostic. The envelope contains:

* Request send datetime (UTC, in epoch).
* Request ID, so that the request sender can correlate the arriving responses.
* ReplyTo topic/queue name, so that the service handling the request knows where to send back the response.
* The body of the request message (serialized using the chosen serialization provider).

### Produce request message

One requirement is that request messages implement the marker interface `IRequestMessage<TResponse>`:

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
// Configure response message queue (or topic) when using req/resp for the request sending side
.ExpectRequestResponses(x =>
{
  x.ReplyToTopic("servicename-instance1"); // All responses from req/resp will return on this topic
  x.DefaultTimeout(TimeSpan.FromSeconds(20)); // Timeout request sender if response won't arrive within 10 seconds.
  //x.Group("some-consumer-group"); // Kafka provider specific setting
})
```

The request sending side declares the request message using `Produce<TMessage>()` method:

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

// or rely on the default topic (or queue) name
var res = await bus.Send(req);
```

### Consume the request message (the request handler)

The request handling micro-service needs to have a handler that implements `IRequestHandler<SomeRequest, SomeResponse>`:

```cs
public class SomeRequestHandler : IRequestHandler<SomeRequest, SomeResponse>
{
  public async Task<MultiplyResponse> OnHandle(MultiplyRequest request, string name)
  {
    // handle the request  
    return new SomeResponse("ping");
  }
}
```

The handler needs to be registered in the DI container. SMB will ask the DI to provide the handler instances when needed.

Configuration of the request message handling is done using the `Handle<TRequest, TResponse>()` method:

```cs
mbb.Handle<SomeRequest, SomeResponse>(x => x
    .Topic("do-some-computation-topic") // Topic to expect the requests on
    .WithHandler<SomeRequestHandler>()
    .Group("some-consumer-group") // kafka provider specific
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
* [Redis](provider_redis.md)
* [Memory](provider_memory.md)
