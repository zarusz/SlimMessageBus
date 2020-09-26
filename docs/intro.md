
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
    //.Group(consumerGroup) // Kafka provider specific (Kafka consumer group name)
  )

  // Req/Resp example:
  .Produce<MultiplyRequest>(x => x.DefaultTopic("multiply-request")) // By default AddCommand messages will go to 'multiply-request' topic (or hub name when Azure Service Hub provider)
  .Handle<MultiplyRequest, MultiplyResponse>(x => x
    .Topic("multiply-request") // Topic to expect the request messages
    .WithHandler<MultiplyRequestHandler>()
    //.Group(consumerGroup) // Kafka provider specific (Kafka consumer group name)
  )
  // Configure response message queue (on topic) when using req/resp
  .ExpectRequestResponses(x =>
  {
    x.ReplyToTopic(topicForResponses); // All responses from req/resp will return on this topic (the EventHub name)
    x.DefaultTimeout(TimeSpan.FromSeconds(20)); // Timeout request sender if response won't arrive within 10 seconds.
    //x.Group(responseGroup); // Kafka provider specific (Kafka consumer group)
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

> The `IMessageBus` is disposable (implements `IDisposable`).


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

#### Producer hooks

When you need to intercept a message that is being published or sent via the bus, you can use the available producer hooks:

```cs
mbb
   .Produce<SomeMessage>(x =>
   {
      x.DefaultTopic(someMessageTopic);
      x.AttachEvents(events =>
      {
         // Invoke the action for the specified message type published/sent via the bus:
         events.OnMessageProduced = (bus, producerSettings, message, name) => {
            Console.WriteLine("The SomeMessage: {0} was sent to topic/queue {1}", message, name);
         }
      });
   })
   .AttachEvents(events =>
   {
      // Invoke the action for any message type published/sent via the bus:
      events.OnMessageProduced = (bus, producerSettings, message, name) => {
         Console.WriteLine("The message: {0} was sent to topic/queue {1}", message, name);
      };
   });
```

The hook can be applied at the specified producer, or the whole bus.

> The user specified `Action<>` methods need to be thread-safe.


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

#### Consumer hooks

When you need to intercept a message that is delivered to a consumer, you can use the available consumer hooks:

```cs
mbb
   .Consume<SomeMessage>(x =>
   {
       x.Topic("some-topic");
       x.AttachEvents(events =>
       {
          // Invoke the action for the specified message type when arrived on the bus:
          events.OnMessageArrived = (bus, consumerSettings, message, name) => {
             Console.WriteLine("The SomeMessage: {0} arrived on the topic/queue {1}", message, name);
          }
          events.OnMessageFault = (bus, consumerSettings, message, ex) => {

          };
       });
    })
    .AttachEvents(events =>
    {
        // Invoke the action for the specified message type when sent via the bus:
        events.OnMessageArrived = (bus, consumerSettings, message, name) => {
           Console.WriteLine("The message: {0} arrived on the topic/queue {1}", message, name);
        };
        events.OnMessageFault = (bus, consumerSettings, message, ex) => {

        };
   });
```

The hook can be applied at the specified consumer, or the whole bus.

> The user specified `Action<>` methods need to be thread-safe.

#### Consumer context

The consumer can access the `ConsumerContext` object which enable the chosen transport provider to pass additional message information specific to the chosen transport. Examples of such information are the Azure Service Bus UserProperties, or Kafka Topic-Partition offset.

To get access the consumer has to implement the `IConsumerContextAware` interface:

```cs
public class PingConsumer : IConsumer<PingMessage>, IConsumerContextAware
{
   public AsyncLocal<ConsumerContext> Context { get; } = new AsyncLocal<ConsumerContext>();

   public Task OnHandle(PingMessage message, string name)
   {
      var messageContext = Context.Value;

      // Kafka transport specific extension:
      var transportMessage = messageContext.GetTransportMessage();
      var partition = transportMessage.TopicPartition.Partition;
   }
}
```

Please consult the individual transport provider documentation to see what is available.

## Request-response communication

SMB provides implementation of request-response over topics or queues - depending what the underlying provider supports.
This allows to asynchronously await a response for a request message that your service sent.

Typically this simplifies service interactions that need to await for a result. To make your app scallable, there is no need to rewrite the interaction as fire and forget style, with storing the state, and writing another consumer that resumes processing when the response arrives.

### Delivery quarantees

Since the response is asynchronously awaited, upon arrival the processing context is available (references to objects, other tasks etc) and the service can resume right away. On top of that, since the interaction is asynchronous this allows to achive a reactive architecture that can scale out - the request handling service will process when available, and the requesting service releases any threads. 

The only overhead is memory and resources kept while the sender is awaiting for the response. There is an easy way to set timeouts for request messages in case the response would not arrive in an expected window. Timeout is configured globally, or per each request message type.

> Please note that if the sending service instance dies while awaiting a response, then after restart the service instance won't resume from that await point as all the context and TPL task will be long gone. If you cannot affort for this to happen consider using [Saga pattern](https://microservices.io/patterns/data/saga.html) instead of request-response.

### Dedicated reply queue/topic

The implementation requires that each micro-service instance that intends to sends request messages needs to have its *own and dedicated* queue (or topic) onto which other request handling micro-services send the response once they process the request.

> It is important that each request sending service instance has its own dedicated topic (or queue) for receiving replies. Please also consult the transport provider documentation too.

### Envelope

Beside the request (or reponse) message payload, the SMB implementation needs to pass additional metadata information to make the request-response work.
Since, SMB needs to work across different transport providers and some do not support message headers, each request (or response) message is wrapped by SMB into a special envelope which makes the implementation transport provider agnostic. 

The envelope is binary and should be fast. See the [`MessageWithHeadersSerializer`](https://github.com/zarusz/SlimMessageBus/blob/master/src/SlimMessageBus.Host/RequestResponse/MessageWithHeadersSerializer.cs) for low level details.

The request envelope contains:

* Request ID, so that the request sender can correlate the arriving responses.
* Request send datetime (UTC, in epoch).
* ReplyTo topic/queue name, so that the service handling the request knows where to send back the response.
* The body of the request message (serialized using the chosen serialization provider).

The response envelope contains:

* Request ID, so that the request sender can correlate the arriving responses.
* Error message, in case the request message processing failed (so that we can fail fast and know the particular error).

In the future SMB might introduce an optimization that will leverage transport native headers (e.g. UserProperties for Azure Service Bus) to avoid the envelope altogether.

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
  public async Task<SomeResponse> OnHandle(SomeRequest request, string name)
  {
    // handle the request  
    return new SomeResponse();
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

## Static accessor

The static `MessageBus.Current` was introduced to obtain either the singleton `IMessageBus` or the current scoped instance (like the `IMessageBus` tied with the currently executing request).

This helps to lookup the `IMessageBus` instance in the domain model layer while doing Domain-Driven Design and specifically to implement domain events or to externalize infrastructure concerns if anything changes on the domain that would require communication with external systems.

See [`DomainEvents`](../src/Samples/Sample.DomainEvents.WebApi/Startup.cs#L79) sample how to configure it per-request scope and how to use it for domain events.

## Dependency resolver

SMB uses dependency resolver to obtain instances of the declared consumers (class instances that implement `IConsumer<>`, `IHandler<>`).
There are few plugins availble that allow to integrate SMB with your favorite DI framework. 

See samples and the [Packages section](../#Packages).

## Serialization

SMB uses serialization plugins to serialize (and deserialize) the messages into the desired format.

See [Serialization](serialization.md) page.

## Logging

SlimMessageBus uses [Microsoft.Extensions.Logging.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions).

The `ILoggerFactory` will be resolved from the dependency injecton container or it can be taken from the `MessageBusBuilder` configuraton:

```cs
ILoggerFactory loggerFactory;    

IMessageBus messageBus = MessageBusBuilder
    .Create()
    // other setup
    .WithLoggerFacory(loggerFactory)
    // other setup
    .Build();
```

When the `ILoggerFactory` is not configured nor available in the DI container SMB will use `NullLoggerFactory.Instance`.
The `.WithLoggerFactory(...)` takes takes precedence over the instance available in the DI container.

## Provider specific functionality

Providers introduce more settings and some subtleties to the above documentation.
For example Apache Kafka requires `mbb.Group(string)` for consumers to declare the consumer group, Azure Service Bus uses `mbb.SubscriptionName(string)` to set subscription name of the consumer, while Memory provider does not use anything like it.

Providers:
* [Apache Kafka](provider_kafka.md)
* [Azure Service Bus](provider_azure_servicebus.md)
* [Azure Event Hubs](provider_azure_eventhubs.md)
* [Redis](provider_redis.md)
* [Memory](provider_memory.md)
