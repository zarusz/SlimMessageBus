# Introduction to SlimMessageBus <!-- omit in toc -->

- [Configuration](#configuration)
- [Pub/Sub communication](#pubsub-communication)
  - [Producer](#producer)
    - [Polymorphic messages](#polymorphic-messages)
    - [Producer hooks](#producer-hooks)
    - [Set message headers](#set-message-headers)
  - [Consumer](#consumer)
    - [Start or Stop message consumption](#start-or-stop-message-consumption)
    - [Consumer hooks](#consumer-hooks)
    - [Consumer context](#consumer-context)
      - [Get message headers in the consumer or handler](#get-message-headers-in-the-consumer-or-handler)
    - [Per-message DI container scope](#per-message-di-container-scope)
    - [Hybrid bus and message scope reuse](#hybrid-bus-and-message-scope-reuse)
    - [Concurrently processed messages](#concurrently-processed-messages)
- [Request-response communication](#request-response-communication)
  - [Delivery quarantees](#delivery-quarantees)
  - [Dedicated reply queue/topic](#dedicated-reply-queuetopic)
  - [Message headers for request-response](#message-headers-for-request-response)
  - [Produce request message](#produce-request-message)
  - [Consume the request message (the request handler)](#consume-the-request-message-the-request-handler)
- [Static accessor](#static-accessor)
- [Dependency resolver](#dependency-resolver)
  - [MsDependencyInjection](#msdependencyinjection)
    - [ASP.Net Core](#aspnet-core)
  - [Autofac](#autofac)
  - [Unity](#unity)
  - [Modularization of configuration](#modularization-of-configuration)
  - [Autoregistration of consumers, interceptors and configurators](#autoregistration-of-consumers-interceptors-and-configurators)
- [Serialization](#serialization)
- [Message Headers](#message-headers)
  - [Message Type Resolver](#message-type-resolver)
- [Interceptors](#interceptors)
  - [Producer Lifecycle](#producer-lifecycle)
  - [Consumer Lifecycle](#consumer-lifecycle)
  - [Order of Execution](#order-of-execution)
- [Logging](#logging)
- [Provider specific functionality](#provider-specific-functionality)

## Configuration

The configuration starts with `MessageBusBuilder`, which allows configuring a couple of elements:

- The bus transport provider (Apache Kafka, Azure Service Bus, Memory).
- The serialization provider.
- The dependency injection provider.
- Declaration of messages produced and consumed along with topic/queue names.
- Request-response configuration (if enabled).
- Additional provider-specific settings (message partition key, message id, etc).

Here is a sample:

```cs
IServiceProvider serviceProvider;

var mbb = MessageBusBuilder.Create()
  
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

The builder is the blueprint for creating message bus instances `IMessageBus`:

```cs
// Build the bus from the builder. 
// Message consumers will start consuming messages from the configured topics/queues of the chosen provider.
IMessageBus bus = mbb.Build();
```

In most scenarios having a singleton `IMessageBus` for your entire application will be sufficient. The provider implementations are thread-safe.

> The `IMessageBus` is disposable (implements `IDisposable` and `IAsyncDisposable`).

When your service uses `Microsoft.Extensions.DependencyInjection`, 
the SMB can be configured in a more compact way (requires `SlimMessageBus.Host.MsDependencyInjection` or `SlimMessageBus.Host.AspNetCore` package):

```cs
// Startup.cs:

IServiceCollection services;

services.AddSlimMessageBus((mbb, svp) =>
  {
    mbb
      .Produce<SomeMessage>(x => x.DefaultTopic("some-topic"))
      // ...
      .WithProviderKafka(new KafkaMessageBusSettings("localhost:9092"));
  });
```

The `.WithDependencyResolver(...)` is already called on the `MessageBusBuilder`.
The `svp` (of type `IServiceProvider`) can be used to obtain additional dependencies from DI.

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

> The transport plugins might introduce additional configuration options. Please check the relevant provider docs. For example, Azure Service Bus, Azure Event Hub and Kafka allow setting the partitioning key for a given message type.

#### Polymorphic messages

Given the following message types:

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

> The [Interceptors](#interceptors) is a newer approach that should be used instead of the hooks.

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

The hook can be applied at the specified producer or the whole bus.

> The user-specified `Action<>` methods need to be thread-safe.

#### Set message headers

> Since version 1.15.0

Whenever the message is published (or sent in request-response), headers can be set to pass additional information with the message:

```cs
await bus.Publish(new CustomerEvent { }, headers: new Dictionary<string, object> { ["CustomerId"] = 1234 });
```

It is also possible to specify a producer-wide modifier for message headers. This can be used if you need to add some specific headers for every message.

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

#### Start or Stop message consumption

By default message consumers are started as soon as the bus is created. This means that messages arriving on the given transport will be processed by the declared consumers.
If you want to prevent this default use the follwing setting:

```cs
mbb.AutoStartConsumersEnabled(false); // default is true
```

Later inject `IMessageBus` and cast it to `IConsumerControl` which exposes a `Start()` and `Stop()` methods to respectively start message consumers or stop them.

```cs
IMessageBus bus = // injected

IConsumerControl consumerControl = (IConsumerControl)bus; // Need to reference SlimMessageBus.Host package

// Start message consumers
await consumerControl.Start();

// or 

// Stop message consumers
await consumerControl.Stop();
```

> Since version 1.15.5

#### Consumer hooks

> The [Interceptors](#interceptors) is a newer approach that should be used instead of the hooks.

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
      // Kafka transport specific extension (requires SlimMessageBus.Host.Kafka package):
      var transportMessage = Context.GetTransportMessage();
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

For more advanced scenarios (third-party plugins) the SMB runtime provides a static accessor `MessageScope.Current` which allows getting ahold of the message scope for the currently running consumer instance.

#### Hybrid bus and message scope reuse

In the case of using the hybrid message bus (`SlimMessageBus.Host.Hybrid` package) you might have a setup where there are two or more bus instances.
For example, the Azure Service Bus (ASB) might be used to consume messages arriving to your service (by default each arriving message will have a DI scope created) and the memory bus (`SlimMessageBus.Host.Memory`) to implement domain events.
In this scenario, the arriving message on ASB would create a message scope, and as part of message handling your code might raise some domain events (in process messages) via the Memory bus.
Here the memory bus would detect there is a message scope already started and will use that to resolve its domain handlers/consumers and required dependencies.

> In a Hybrid bus setup, the Memory bus will detect if there is already a started message scope and use that to resolve its dependencies from.

#### Concurrently processed messages

The `.Instances(n)` allows setting the number of concurrently processed messages within the same consumer type.

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

SMB provides an implementation of request-response over topics or queues - depending on what the underlying provider supports.
This allows one to asynchronously await a response for a request message that your service sent.

Typically this simplifies service interactions that need to wait for a result. To make your app scallable, there is no need to rewrite the interaction as fire and forget style, with storing the state and writing another consumer that resumes processing when the response arrives.

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

- `RequestId` (string) so that the request sender can correlate the arriving responses.
- `Expires` (long) which is a datetime (8 bytes long type, UTC, expressed in unix epoch) of when the request message (expires on).
- `ReplyTo` (string) topic/queue name, so that the service handling the request knows where to send back the response.
- The body of the request message (serialized using the chosen serialization provider).

The response contains headers:

- `RequestId` (string) so that the request sender can correlate the arriving responses.
- `Error` (string) message, in case the request message processing failed (so that we can fail fast and know the particular error).

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

The static `MessageBus.Current` was introduced to obtain the `IMessageBus` from the current context. The bus will typically be a singleton `IMessageBus`. However, the consumer instances will be obtained from the current DI scope (tied with the current web request or message scope when in a message handling scope).

This allows to easily look up the `IMessageBus` instance in the domain model layer methods when doing Domain-Driven Design and specifically to implement domain events. This pattern allows externalizing infrastructure concerns (domain layer sends domain events when anything changes on the domain that would require communication to other layers or external systems).

For ASP.NET Core application, in the `Startup.cs` configure the accessor like this:

```cs
public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
  // ...

  // Set the MessageBus provider to resolve the IMessageBus from the current request scope (or root if not inside a request scope)  
  MessageBus.SetProvider(MessageBusCurrentProviderBuilder.Create().From(app).Build()); // Requires SlimMessageBus.Host.AspNetCore package
}
```

For a console app, simply pass the root `IServiceProvider` like this:

```cs
IServiceProvider svp;

// Resolve the IMessageBus from the root container
MessageBus.SetProvider(MessageBusCurrentProviderBuilder.Create().From(svp).Build());
```

See [`DomainEvents`](../src/Samples/Sample.DomainEvents.WebApi/Startup.cs#L79) sample how to configure it per-request scope and how to use it for domain events.

## Dependency resolver

SMB uses a dependency resolver to obtain instances of the declared consumers (class instances that implement `IConsumer<>` or `IHandler<>`).
There are a few plugins available that allow you integrating SMB with your favorite DI library.

The consumer/handler is typically resolved from DI container when the message arrives and needs to be handled.
SMB does not maintain a reference to that object instance after consuming the message - this gives user the ability to decide if the consumer/handler should be a singleton, transient, or scoped (to the message being processed or ongoing web-request) and when it should be disposed of.

The disposal of the consumer instance obtained from the DI is typically handled by the DI (if the consumer implements `IDisposable`).
By default, SMB creates a child DI scope for every arriving message (`.IsMessageScopeEnabled(true)`). After the message is processed,
SMB disposes of that child DI scope. With that, the DI will dispose of the consumer instance and its injected collaborators.

Now, in some special situations, you might want SMB to dispose of the consumer instance
after the message has been processed - you can enable that with `.DisposeConsumerEnabled(true)`. 
This setting will make SMB dispose of the consumer instance if only it implements the `IDisposable` interface.

> It is recommended to leave the default per-message scope creation, and register the consumer types/handlers as either transient or scoped.

### MsDependencyInjection

The [`MsDependencyInjection`](https://www.nuget.org/packages/SlimMessageBus.Host.MsDependencyInjection) plugin (or [`AspNetCore`](https://www.nuget.org/packages/SlimMessageBus.Host.AspNetCore)) introduces several conveniences to configure the bus.
The `.AddSlimMessageBus()` extension configures the message bus and registers the SMB types with the container. For example:

```cs
IServiceCollection services;

services.AddSlimMessageBus((mbb, svp) =>
  {
    mbb
      .Produce<SomeMessage>(x => x.DefaultTopic("some-topic"))
      // ...
      .WithProviderKafka(new KafkaMessageBusSettings("localhost:9092"));
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

The `svp` (of type `IServiceProvider`) parameter can be used to obtain additional dependencies from DI.

> The `.WithDependencyResolver(...)` is already called on the `MessageBusBuilder`.

#### ASP.Net Core

For ASP.NET services, it is recommended to use the [`AspNetCore`](https://www.nuget.org/packages/SlimMessageBus.Host.AspNetCore) plugin. To properly support request scopes it has a dependency on the `IHttpContextAccessor` which [needs to be registered](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/http-context?view=aspnetcore-6.0#use-httpcontext-from-custom-components) during application setup:

```cs
// This will register the `IHttpContextAccessor`
services.AddHttpContextAccessor();
```

### Autofac

The [`Autofac`](https://www.nuget.org/packages/SlimMessageBus.Host.Autofac) plugin introduces the `SlimMessageBusModule` [autofac module](https://autofac.readthedocs.io/en/latest/configuration/modules.html) that configures message bus and registers SMB types within the DI container. For example:

```cs
ContainerBuilder builder;

builder.RegisterModule(new SlimMessageBusModule
{
   ConfigureBus = (mbb, ctx) =>
   {
      mbb
         .Produce<SomeMessage>(x => x.DefaultTopic("some-topic"))
         .Consume<SomeMessage>(x => x
            .Topic("some-topic")
            .WithConsumer<SomeMessageConsumer>()
            //.KafkaGroup("some-kafka-consumer-group") //  Kafka provider specific
            //.SubscriptionName("some-azure-sb-topic-subscription") // Azure ServiceBus provider specific
         )
         // ...
         .WithSerializer(new JsonMessageSerializer())
         .WithProviderKafka(new KafkaMessageBusSettings("localhost:9092"));
         // Use Azure Service Bus transport provider
         //.WithProviderServiceBus(...)
         // Use Azure Azure Event Hub transport provider
         //.WithProviderEventHub(...)
         // Use Redis transport provider
         //.WithProviderRedis(...)
         // Use in-memory transport provider
         //.WithProviderMemory(...)         
   },
   AddConsumersFromAssembly: new[] { Assembly.GetExecutingAssembly() }, // auto discover consumers and register into DI (see next section)
   AddInterceptorsFromAssembly: new[] { Assembly.GetExecutingAssembly() }, // auto discover interceptors and register into DI (see next section)
   AddConfiguratorsFromAssembly: new[] { Assembly.GetExecutingAssembly() } // auto discover modular configuration and register into DI (see next section)
});
```

The `ctx` (of type `IComponentContext`) parameter can be used to obtain additional dependencies from DI.

> The `.WithDependencyResolver(...)` is already called on the `MessageBusBuilder`.

### Unity

The [`Unity`](https://www.nuget.org/packages/SlimMessageBus.Host.Unity) plugin introduces the `.AddSlimMessageBus()` extension method. It enables to configure the message bus and register relevant types for the SMB. For example:

```cs
IUnityContainer container;

container.AddSlimMessageBus((mbb, container) =>
  {
    mbb
      .Produce<SomeMessage>(x => x.DefaultTopic("some-topic"))
      // ...
      .WithProviderKafka(new KafkaMessageBusSettings("localhost:9092"));
  }, 
  // Optional:
  addConsumersFromAssembly: new[] { Assembly.GetExecutingAssembly() }, // auto discover consumers and register into DI (see next section)
  addInterceptorsFromAssembly: new[] { Assembly.GetExecutingAssembly() }, // auto discover interceptors and register into DI (see next section)
  addConfiguratorsFromAssembly: new[] { Assembly.GetExecutingAssembly() } // auto discover modular configuration and register into DI (see next section)
);
```

The `container` (of type `IUnityContainer`) parameter can be used to obtain additional dependencies from DI.

> The `.WithDependencyResolver(...)` is already called on the `MessageBusBuilder`.

### Modularization of configuration

> Since version 1.6.4

If you want to avoid configuring the bus all in one place (`Startup.cs`) and rather have modules of the application 
responsible for configuring their consumers or producers then it can be done using an implementation of `IMessageBusConfigurator` that is placed in each module (assembly).

```cs
public MyAppModule : IMessageBusConfigurator
{
    public MyAppModule(/* dependencies injected by DI */) { }

    public void Configure(MessageBusBuilder mbb, string busName) 
    {
        mbb
            .Produce<SomeMessage>(...)
            .Consume<SomeMessage>(...)
    }
}
```

Implementations of `IMessageBusConfigurator` registered in the DI will be resolved and used to configure the message bus as well as any child bus that was declared (see [Hybrid docs](provider_hybrid.md#configuration-modularization)).
The `busName` parameter is mostly relevant if you are using the Hybrid bus transport.
The `mbb` parameter represents the builder for the bus.

When using `MsDependencyInjection` for DI, we can use `AddMessageBusConfiguratorsFromAssembly` extension method (in Startup.cs) to search for any implementations of `IMessageBusConfigurator` and register them as transient with the container:

```cs
var accountingModuleAssembly = Assembly.GetExecutingAssembly();
services.AddMessageBusConfiguratorsFromAssembly(accountingModuleAssembly);
```

The other DI plugns (Unity, Autofac) provide similar means for discovery of configuration types.

### Autoregistration of consumers, interceptors and configurators

> Since version 1.6.4

We can also use the `AddMessageBusConsumersFromAssembly` extension method to search for any implementations of `IConsumer<T>` (or `IRequestHandler<T, R>`) and register them as Transient with the container:

```cs
services.AddMessageBusConsumersFromAssembly(Assembly.GetExecutingAssembly());
```

## Serialization

SMB uses serialization plugins to serialize (and deserialize) the messages into the desired format.

See [Serialization](serialization.md) page.

## Message Headers

SMB uses headers to pass additional metadata information with the message. This includes the `MessageType` (of type `string`) or in the case of request/response messages the `RequestId` (of type `string`), `ReplyTo` (of type `string`) and `Expires` (of type `long`).
Depending on the underlying transport chosen the headers will be supported natively by the underlying message system/broker (Azure Service Bus, Azure Event Hubs, Kafka) or emulated (Redis).

The emulation works by using a message wrapper envelope (`MessageWithHeader`) that during serialization puts the headers first and then the actual message content after that. Please consult individual transport providers.

### Message Type Resolver

By default, the message header `MessageType` conveys the message type information using the assembly qualified name of the .NET type (see `AssemblyQualifiedNameMessageTypeResolver`).

The following can be used to provide a custom `IMessageTypeResolver` implementation:

```cs
IMessageTypeResolver mtr = new AssemblyQualifiedNameMessageTypeResolver();

mbb.WithMessageTypeResolver(mtr)
```

A custom resolver could be used in scenarios when there is a desire to send short type names (to optimize overall message size). In this scenario the assembly name and/or namespace could be skipped - the producer and consumer could infer them.

## Interceptors

Interceptors allow to tap into the message processing pipeline on both the producer and consumer sides. Sample use cases for interceptors include:

- decorate the producer side with adding additional validation prior the message is sent,
- add custom logging for a given type of message,
- modify or augment the original response message or provide a different response,
- prevent a message from being produced or from being consumed or handled,
- perform some additional application specific authorization checks.

### Producer Lifecycle

When a message is produced (via the `bus.Publish(message)` or `bus.Send(request)`) the SMB is performing a DI lookup for the interceptor interface types that are relevant given the message type (or request and response types) and execute them in order.

```cs
// Will intercept bus.Publish() and bus.Send()
public interface IProducerInterceptor<in TMessage>
{
   Task<object> OnHandle(TMessage message, CancellationToken cancellationToken, Func<Task<object>> next, IMessageBus bus, string path, IDictionary<string, object> headers);
}

// Will intercept bus.Publish()
public interface IPublishInterceptor<in TMessage>
{
   Task OnHandle(TMessage message, CancellationToken cancellationToken, Func<Task> next, IMessageBus bus, string path, IDictionary<string, object> headers);
}

// Will intercept bus.Send()
public interface ISendInterceptor<in TRequest, TResponse>
{
   Task<TResponse> OnHandle(TRequest request, CancellationToken cancellationToken, Func<Task<TResponse>> next, IMessageBus bus, string path, IDictionary<string, object> headers);
}
```

> Remember to register your interceptor types in the DI (either using auto-discovery [`addInterceptorsFromAssembly`](#MsDependencyInjection) or manually).

> SMB has an optimization that will remember the types of messages for which the DI resolved interceptor. That allows us to avoid having to perform lookups with the DI and other internal processing.

### Consumer Lifecycle

On the consumer side, before the recieved message is delivered to the consumer (or request handler) the SMB is performing a DI lookup for the interceptor interface types that are relevant for the given message type (or request and response type).

```cs
// Intercepts consumers of type IConsumer<TMessage> and IRequestHandler<TMessage, TResponse>
public interface IConsumerInterceptor<in TMessage>
{
   Task OnHandle(TMessage message, CancellationToken cancellationToken, Func<Task> next, IMessageBus bus, string path, IReadOnlyDictionary<string, object> headers, object consumer);
}

// Intercepts consumers of type IRequestHandler<TMessage, TResponse>
public interface IRequestHandlerInterceptor<in TRequest, TResponse> : IInterceptor
{
   Task<TResponse> OnHandle(TRequest request, CancellationToken cancellationToken, Func<Task<TResponse>> next, IMessageBus bus, string path, IReadOnlyDictionary<string, object> headers, object handler);
}
```

> Remember to register your interceptor types in the DI (either using auto-discovery [`addInterceptorsFromAssembly`](#MsDependencyInjection) or manually).

### Order of Execution

The interceptors are invoked in order from generic to more specific (`IProducerInterceptor<T>` then `IPublishInterceptor<T>` for publish) and in a chain one after another, as long as the `await next()` is called by the previous interceptor. The final `next` delegate is the actual message production to the underlying bus transport.

> When an interceptor avoids to execute `next` delegate, the subsequent interceptors are not executed nor does the message processing hapen (production or consumption).

In case of multiple interceptors that match a particular message type, and when their order of execution has to overriden the interceptor type could implement the `IInterceptorWithOrder` interface to influence the order of execution in the chain:

```cs
public class PublishInterceptorFirst: IPublishInterceptor<SomeMessage>, IInterceptorWithOrder
{    
   public int Order => 1;

   public Task OnHandle(SomeMessage message, CancellationToken cancellationToken, Func<Task> next, IMessageBus bus, string path, IDictionary<string, object> headers) { }
}

public class PublishInterceptorSecond: IPublishInterceptor<SomeMessage>, IInterceptorWithOrder
{    
   public int Order => 2;

   public Task OnHandle(SomeMessage message, CancellationToken cancellationToken, Func<Task> next, IMessageBus bus, string path, IDictionary<string, object> headers) { }
}
```

## Logging

SlimMessageBus uses [Microsoft.Extensions.Logging.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions).

The `ILoggerFactory` will be resolved from the dependency injection container or it can be taken from the `MessageBusBuilder` configuration:

```cs
ILoggerFactory loggerFactory;    

mbb // of type MessageBusBuilder
  .WithLoggerFacory(loggerFactory)
```

When the `ILoggerFactory` is not configured nor available in the DI container SMB will use `NullLoggerFactory.Instance`.
The `.WithLoggerFactory(...)` takes takes precedence over the instance available in the DI container.

## Provider specific functionality

Providers introduce more settings and some subtleties to the above documentation.
For example, Apache Kafka requires `mbb.KafkaGroup(string)` for consumers to declare the consumer group, Azure Service Bus uses `mbb.SubscriptionName(string)` to set the subscription name of the consumer, while the Memory provider does not use anything like it.

Providers:

- [Apache Kafka](provider_kafka.md)
- [Azure Service Bus](provider_azure_servicebus.md)
- [Azure Event Hubs](provider_azure_eventhubs.md)
- [Redis](provider_redis.md)
- [Memory](provider_memory.md)
- [Hybrid](provider_hybrid.md)
