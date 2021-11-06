
# Introduction to SlimMessageBus

## Configuration

The configuration starts with `MessageBusBuilder`, which allows to configure couple of elements:

* The bus transport provider (Apache Kafka, Azure Service Bus, Memory).
* The serialization provider.
* The dependency injection provider.
* Declaration of messages produced and consumed along with topic/queue names.
* Request-response configuration (if enabled).
* Additional provider specific settings (message partition key, message id, etc).

Here is a sample:

```cs
IServiceProvider serviceProvider;

var mbb = MessageBusBuilder
   .Create()
  
   // Use JSON for message serialization
  .WithSerializer(new JsonMessageSerializer())
  
  // Use DI from ASP.NET Core
  .WithDependencyResolver(new AspNetCoreMessageBusDependencyResolver(serviceProvider))
  //.WithDependencyResolver(new MsDependencyInjectionDependencyResolver(serviceProvider))
  
  // Use the Kafka Provider
  .WithProviderKafka(new KafkaMessageBusSettings("localhost:9092"));

  // Pub/Sub example:
  .Produce<AddCommand>(x => x.DefaultTopic("add-command")) // By default AddCommand messages will go to 'add-command' topic (or hub name when Azure Service Hub provider)
  .Consume<AddCommand>(x => x
    .Topic("add-command")
    .WithConsumer<AddCommandConsumer>()
    //.KafkaGroup(consumerGroup) // Kafka provider specific (Kafka consumer group name)
  )

  // Req/Resp example:
  .Produce<MultiplyRequest>(x => x.DefaultTopic("multiply-request")) // By default AddCommand messages will go to 'multiply-request' topic (or hub name when Azure Service Hub provider)
  .Handle<MultiplyRequest, MultiplyResponse>(x => x
    .Topic("multiply-request") // Topic to expect the request messages
    .WithHandler<MultiplyRequestHandler>()
    //.KafkaGroup(consumerGroup) // Kafka provider specific (Kafka consumer group name)
  )
  // Configure response message queue (on topic) when using req/resp
  .ExpectRequestResponses(x =>
  {
    x.ReplyToTopic(topicForResponses); // All responses from req/resp will return on this topic (the EventHub name)
    x.DefaultTimeout(TimeSpan.FromSeconds(20)); // Timeout request sender if response won't arrive within 10 seconds.
    //x.KafkaGroup(responseGroup); // Kafka provider specific (Kafka consumer group)
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
         events.OnMessageProduced = (bus, producerSettings, message, path) => {
            Console.WriteLine("The SomeMessage: {0} was sent to topic/queue {1}", message, path);
         }
      });
   })
   .AttachEvents(events =>
   {
      // Invoke the action for any message type published/sent via the bus:
      events.OnMessageProduced = (bus, producerSettings, message, path) => {
         Console.WriteLine("The message: {0} was sent to topic/queue {1}", message, path);
      };
   });
```

The hook can be applied at the specified producer, or the whole bus.

> The user specified `Action<>` methods need to be thread-safe.

#### Set message headers when producing message

> Since version 1.15.0

Whenever the message is published (or sent in request-response), headers can be set to pass additional information with the message:

```cs
await bus.Publish(new CustomerEvent { }, headers: new Dictionary<string, object> { ["CustomerId"] = 1234 });
```

It is also possible to specify a producer wide modifier for message headers. This can be used if you need to add some specific headers for every message.

```cs
mbb
   .Produce<SomeMessage>(x =>
   {
      x.DefaultTopic(someMessageTopic);
      x.WithHeaderModifier((headers, message) =>
      {
          headers["CustomerId"] = message.CustomerId;
      });
   })
```

Finally, it is possible to specify a headers modifier for the entire bus:

```cs
mbb
   x.WithHeaderModifier((headers, message) =>
   {
      headers["Source"] = "Customer-MicroService";
   })
```

### Consumer

To consume a message type from a topic/queue, declare it using `Consume<TMessage>()` method:

```cs
mbb.Consume<SomeMessage>(x => x
  .Topic("some-topic") // or queue name
  .WithConsumer<SomeConsumer>() // (1)
  // if you do not want to implement the IConsumer<T> interface
  // .WithConsumer<AddCommandConsumer>(nameof(AddCommandConsumer.MyHandleMethod)) // (2) uses reflection
  // .WithConsumer<AddCommandConsumer>((consumer, message, path) => consumer.MyHandleMethod(message, path)) // (3) uses a delegate
  .Instances(1)
  //.KafkaGroup("some-consumer-group")) // Kafka provider specific extensions
```

When the consumer implements the `IConsumer<SomeMessage>` interface:

```cs
public class SomeConsumer : IConsumer<SomeMessage>
{
  public async Task OnHandle(SomeMessage msg, string path)
  {
    // handle the msg
  }
}
```

The second parameter (`path`) is the topic (or queue) name that the message arrived on.

The `SomeConsumer` needs to be registered in your DI container. The SMB runtime will ask the chosen DI to provide the desired number of consumer instances. Any collaborators of the consumer will be resolved according to your DI configuration.

Alternatively, if you do not want to implement the `IConsumer<SomeMessage>`, then you can provide the method name (2) or a delegate that calls the consumer method (3):

```cs
public class SomeConsumer
{
  public async Task MyHandleMethod(SomeMessage msg, string path)
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
       // This events trigger only for this consumer
       x.AttachEvents(events =>
       {
          // 1. Invoke the action for the specified message type when arrived on the bus (pre consumer OnHandle method):
          events.OnMessageArrived = (bus, consumerSettings, message, path, nativeMessage) => {
             Console.WriteLine("The SomeMessage: {0} arrived on the topic/queue {1}", message, path);
          }
          
          // 2. Invoke the action when the consumer caused an unhandled exception
          events.OnMessageFault = (bus, consumerSettings, message, ex, nativeMessage) => {
          };
          
          // 3. Invoke the action for the specified message type after consumer processed (post consumer OnHandle method).
          // This is executed also if the message handling faulted (2.)
          events.OnMessageFinished = (bus, consumerSettings, message, path, nativeMessage) => {
             Console.WriteLine("The SomeMessage: {0} finished on the topic/queue {1}", message, path);
          }
       });
    })
    // Any consumer events for the bus (a sum of all events across registered consumers)
    .AttachEvents(events =>
    {
          // Invoke the action for the specified message type when sent via the bus:
          events.OnMessageArrived = (bus, consumerSettings, message, path, nativeMessage) => {
             Console.WriteLine("The message: {0} arrived on the topic/queue {1}", message, path);
          };
          
          events.OnMessageFault = (bus, consumerSettings, message, ex, nativeMessage) => {
          };
          
          events.OnMessageFinished = (bus, consumerSettings, message, path, nativeMessage) => {
             Console.WriteLine("The SomeMessage: {0} finished on the topic/queue {1}", message, path);
          }
   });
```

The hook can be applied for the specified consumer, or for all consumers in the particular bus instance.

> The user specified `Action<>` methods need to be thread-safe as they will be executed concurrently as messages are being processed.

#### Consumer context

> Changed in version 1.15.0

The consumer can access the `IConsumerContext` object which enable the chosen transport provider to pass additional message information specific to the chosen transport.
Examples of such information are the Azure Service Bus UserProperties, or Kafka Topic-Partition offset.

To use it the consumer has to implement the `IConsumerWithContext` interface:

```cs
public class PingConsumer : IConsumer<PingMessage>, IConsumerWithContext
{
   public IConsumerContext Context { get; set; }

   public Task OnHandle(PingMessage message, string path)
   {
      var messageContext = Context.Value;

      // Kafka transport specific extension (requires SlimMessageBus.Host.Kafka package):
      var transportMessage = messageContext.GetTransportMessage();
      var partition = transportMessage.TopicPartition.Partition;
   }
}
```

SMB will set the `Context` property prior calling `OnHandle`.

Please consult the individual transport provider documentation to see what is available.

> It is important that the consumer type is registered as either transient (prototype), scope based (per message), for the `Context` property to work properly.
If the consumer type would be a singleton, then somewhere between the setting of `Headers` and running the `OnHandle` there would be a race condition.

##### Get message headers in the consumer or handler

> Since version 1.15.0

Whenever the consumer type (`IConsumer<T>` or `IRequestHandler<Req, Res>`) requires to obtain message headers for the message being processed, it needs to extend the interface `IConsumerWithContext`. 

```cs
public class SomeConsumer : IConsumer<SomeMessage>, IConsumerWithContext
{
  ppublic IConsumerContext Context { get; set; }

  public async Task OnHandle(SomeMessage msg, string path)
  {
    // the msgessage headers are in the Context.Headers property
  }
}
```


#### Per-message DI container scope

SMB can be configured to create a DI scope for every message being consumed. That is if the chosen DI container supports child scopes.
This allows to have a scoped `IConsumer<T>` or `IRequestHandler<TRequest, TResponse>` which can have any dependant collaborators that are also scoped (e.g. EF Core DataContext).

```cs
IMessageBusBuilder mbb;

mbb.PerMessageScopeEnabled(true); // this will set the default setting for each consumer to create per-message scope in the DI container for every message about to be processed

// SomeConsumer will be resolved from a child scope created for each consumed message - the default bus setting will apply
mbb.Consume<Message>(x => x
  .Topic("topic")
  .WithConsumer<TConsumer>()
);

// AnotherConsumer will be resolved from the root DI for each message
mbb.Consume<Message2>(x => x
  .Topic("topic2")
  .WithConsumer<TConsumer2>()
  .PerMessageScopeEnabled(false) // override the bus global default setting and do not create scope for each message on this consumer
);
```

> Per-message scope is enabled by default for all transports except the in-memory transport. This should work for most scenarios.

For more advanced scenarios (third-party plugins) the SMB runtime provides a static accessor `MessageScope.Current` which allows to get ahold of the message scope for the currently running consumer instance.

#### Concurrently processed messages

The `.Instances(n)` allows to set the `n` number of concurrently processed messages within the same consumer type.

```cs
mbb.Consume<SomeMessage>(x => x
  .Topic("topic")
  .WithConsumer<TConsumer>()
  .Instances(3) // At most there will be 3 instances of messages processsed simultaneously
);
```

> The default is `1`.

Some transports underlying clients do support concurrency natively (like Azure Service Bus), others do not support concurrent message handling (like Redis) in which case SMB will pararellize processing.

SMB manages a critical section for each consumer type registration that ensures there are at most `n` processed messages.
Each processing of a message resolves the `TConsumer` instance from the DI.

> Please note that anything higher than 1 will cause multiple messages to be consumed concurrently in one service instance. This will typically impact message processing order (ie 2nd message might get processed sooner than the 1st message). 

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

### Message headers for request-response

In the case of request (or reponse) message, the SMB implementation needs to pass additional metadata information to make the request-response work (correlate response with a pending request, pass error message back to sender, etc).
For majority of the transport providers SMB leverages the native message headers of the underlying transport (when the transport supports it). In the transports that do not natively support headers (e.g. Redis) each request (or response) message is wrapped by SMB into a special envelope which makes the implementation transport provider agnostic (see type `MessageWithHeaders`). 

When header emulation is taking place, SMB uses a wrapper envelope that is binary and should be fast. See the [`MessageWithHeadersSerializer`](https://github.com/zarusz/SlimMessageBus/blob/master/src/SlimMessageBus.Host/RequestResponse/MessageWithHeadersSerializer.cs) for low level details (or if you want to serialize the wrapper in a different way).

The request contains headers:

* `RequestId` (string) so that the request sender can correlate the arriving responses.
* `Expires` (long) which is a datetime (8 bytes long type, UTC, expressed in unix epoch) of when the request message (expires on).
* `ReplyTo` (string) topic/queue name, so that the service handling the request knows where to send back the response.
* The body of the request message (serialized using the chosen serialization provider).

The response contains headers:

* `RequestId` (string) so that the request sender can correlate the arriving responses.
* `Error` (string) message, in case the request message processing failed (so that we can fail fast and know the particular error).

### Produce request message

The request messages can use the optional marker interface `IRequestMessage<TResponse>`:

```cs
// Option 1:
public class SomeRequest : IRequestMessage<SomeResponse> // Implementing the marker interface is optional
{
}

// Option 2:
// public class SomeRequest // The marker interface is not used (it's not mandatory)
// {
// }

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
  //x.KafkaGroup("some-consumer-group"); // Kafka provider specific setting
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

Once the producer side is configured you can send the request message to a topic (or queue) like so:

```cs
var req = new SomeRequest("ping");
var res = await bus.Send(req, "do-other-computation-topic"); // Option 1 - with marker interface
// var res = await bus.Send<SomeResponse, SomeRequest>(req, "do-other-computation-topic"); // Option 2 - without marker interface

// or rely on the default topic (or queue) name
var res = await bus.Send(req); // Option 1 - with marker interface
// var res = await bus.Send<SomeResponse, SomeRequest>(req); // Option 2 - without marker interface
```

> The marker interface `IRequestMessage<TResponse>` helps to avoid having to specify the request and response types in the `IMessageBus.Send()` method. There is no other difference.

### Consume the request message (the request handler)

The request handling micro-service needs to have a handler that implements `IRequestHandler<SomeRequest, SomeResponse>`:

```cs
public class SomeRequestHandler : IRequestHandler<SomeRequest, SomeResponse>
{
  public async Task<SomeResponse> OnHandle(SomeRequest request, string path)
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
    .KafkaGroup("some-consumer-group") // kafka provider specific
  )
```

> The same micro-service can both send the request and also be the handler of those requests.

## Static accessor

The static `MessageBus.Current` was introduced to obtain either the singleton `IMessageBus` or the current scoped instance (like the `IMessageBus` tied with the currently executing request).

This helps to lookup the `IMessageBus` instance in the domain model layer while doing Domain-Driven Design and specifically to implement domain events or to externalize infrastructure concerns (domain layer sends domain events when anything changes on the domain that would require communication other layers or external systems).

See [`DomainEvents`](../src/Samples/Sample.DomainEvents.WebApi/Startup.cs#L79) sample how to configure it per-request scope and how to use it for domain events.

## Dependency resolver

SMB uses dependency resolver to obtain instances of the declared consumers (class instances that implement `IConsumer<>`, `IHandler<>`).
There are few plugins available that allow to integrate SMB with your favorite DI library. 

The consumer/handler is typically resolved from DI container when the message arrives and needs to be handled. 
SMB does not maintain a reference to that object instance after consuming of the message - this gives user the ability to decide is the consumer/handler should be a singleton, transient, or scoped (to the message being processed or ongoing web-request) and when it should be disposed.

The disposal of the consumer instance obtained by the DI is typically handled by the DI (if the consumer implements `IDisposable`).
By default SMB creates a child DI scope for every arriving message (`.IsMessageScopeEnabled(true)`) and after the message is processed, 
SMB disposes that child DI scope - with that the DI will dispose the consumer instance and its injected collaborators.

Now, in some special situations you might want SMB to dispose the consumer instance 
after the message has been processed - you can enable that with `.DisposeConsumerEnabled(true)`. 
This setting will make SMB dispose the consumer instance if only it implements the `IDisposable` interface.

> Generally its recommended to leave the default per-message scope creation, and register the consumer types/handlers as either transient or scoped.

See more [here](https://docs.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines#general-idisposable-guidelines) on disposable best practices.

See samples and the [Packages section](../#Packages).

## Serialization

SMB uses serialization plugins to serialize (and deserialize) the messages into the desired format.

See [Serialization](serialization.md) page.

## Message Headers

SMB uses headers to pass additional metadata information with the message. This includes the `MessageType` (of type `string`) or in the case of request/response messages the `RequestId` (of type `string`), `ReplyTo` (of type `string`) and `Expires` (of type `long`).
Depending on the underlying transport chosen the headers will be supported natively by the underlying message system/broker (Azure Service Bus, Azure Event Hubs, Kafka) or emulated (Redis).

The emulation works by using a message wrapper envelope (`MessageWithHeader`) that during serialization puts the headers first and then the actual message content after that. Please consult individual transport providers.

### Message Type Resolver

By default the message header `MessageType` conveys the message type information using the assembly qualified name of the .NET type (see `AssemblyQualifiedNameMessageTypeResolver`).

The following can be used to provide a custom `IMessageTypeResolver` implementation:

```cs
IMessageTypeResolver mtr = new AssemblyQualifiedNameMessageTypeResolver();

mbb.WithMessageTypeResolver(mtr)
```

A custom resolver could be used in scenarios when there is a desire to send short type names (to optimize overall message size). In this scenario the assembly name and/or namespace could be skipped - the producer and consumer could infer them.

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
For example Apache Kafka requires `mbb.KafkaGroup(string)` for consumers to declare the consumer group, Azure Service Bus uses `mbb.SubscriptionName(string)` to set subscription name of the consumer, while Memory provider does not use anything like it.

Providers:
* [Apache Kafka](provider_kafka.md)
* [Azure Service Bus](provider_azure_servicebus.md)
* [Azure Event Hubs](provider_azure_eventhubs.md)
* [Redis](provider_redis.md)
* [Memory](provider_memory.md)
