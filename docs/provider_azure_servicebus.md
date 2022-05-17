# Azure Service Bus Provider for SlimMessageBus <!-- omit in toc -->

Please read the [Introduction](intro.md) before reading this provider documentation.

- [Configuration](#configuration)
- [Producing Messages](#producing-messages)
  - [Message modifier](#message-modifier)
- [Consuming Messages](#consuming-messages)
  - [Consumer context](#consumer-context)
  - [Exception Handling for Consumers](#exception-handling-for-consumers)
  - [Transport Specific Settings](#transport-specific-settings)
- [Request-Response Configuration](#request-response-configuration)
  - [Produce Request Messages](#produce-request-messages)
  - [Handle Request Messages](#handle-request-messages)
- [ASB Sessions](#asb-sessions)

## Configuration

Azure Service Bus provider requires a connection string:

```cs
var connectionString = "" // Azure Service Bus connection string

// MessageBusBuilder mbb;
mbb.    
    // the bus configuration here
    .WithProviderServiceBus(new ServiceBusMessageBusSettings(connectionString))
    .WithSerializer(new JsonMessageSerializer());

IMessageBus bus = mbb.Build();
```

The `ServiceBusMessageBusSettings` has additional settings that allow overriding factories for Azure SB client objects. This may be used for some advanced scenarios.

Since Azure SB supports topics and queues, the SMB needs to know whether to produce (or consume) messages to (from) a topic or a queue.
This determination is set as part of the bus builder configuration.

## Producing Messages

To produce a given `TMessage` to a Azure Serivce Bus queue (or topic) use:

```cs
// send TMessage to Azure SB queues
mbb.Produce<TMessage>(x => x.UseQueue()); 

// OR

// send TMessage to Azure SB topics
mbb.Produce<TMessage>(x => x.UseTopic());
```

The above example configures the runtime to deliver all message types of `TMessage` to an Azure Service Bus queue (or topic) respectively.

Then anytime you produce a message like this:

```cs
TMessage msg;

// msg will go to the "some-queue" queue
bus.Publish(msg, "some-queue");

// OR

// msg will go to the "some-topic" topic
bus.Publish(msg, "some-topic");
```

The second (`path`) parameter indicates a queue or topic name - depending on the bus configuration.

When the default queue (or topic) path is configured for a message type:

```cs
mbb.Produce<TMessage>(x => x.DefaultQueue("some-queue"));    

// OR

mbb.Produce<TMessage>(x => x.DefaultTopic("some-topic"));
```

and the second (`path`) parameter is omitted in `bus.Publish()`, then that default queue (or topic) name is going to be used:

```cs
// msg will go to the "some-queue" queue (or "some-topic")
bus.Publish(msg);
```

Setting the default queue name `DefaultQueue()` for a message type will implicitly configure `UseQueue()` for that message type. By default, if configuration is not provided then runtime will assume a message needs to be sent on a topic (and works as if `UseTopic()` was configured).

### Message modifier

Azure SB client's native message type (`Azure.Messaging.ServiceBus.ServiceBusMessage`) allows setting the partition key, message-id, session-id, or additional key-value properties for a message.

SMB supports setting these values via a message modifier (`.WithModifier()`) configuration:

```cs
mbb.Produce<PingMessage>(x =>
{
    x.DefaultTopic(topic);
    // this is optional
    x.WithModifier((message, sbMessage) =>
    {
        // set the Azure SB message ID
        sbMessage.MessageId = $"ID_{message.Counter}";
        // set the Azure SB message partition key
        sbMessage.PartitionKey = message.Counter.ToString();
    });
})
```

> Since version 1.15.5 the Azure SB client was updated, so the native message type is now `Azure.Messaging.ServiceBus.ServiceBusMessage` (it used to be `Azure.ServiceBus.Message`).

## Consuming Messages

To consume `TMessage` by `TConsumer` from `some-topic` Azure Service Bus topic use:

```cs
mbb.Consume<TMessage>(x => x
   .Topic("some-topic")
   .SubscriptionName("subscriber-name")
   .WithConsumer<TConsumer>()
   .Instances(1));
```

Notice the subscription name needs to be provided when consuming from a topic. Furthermore, the subscription has to be created in Azure before running your application (it is not automatically created).

To consume `TMessage` by `TConsumer` from `some-queue` Azure Service Bus queue use:

```cs
mbb.Consume<TMessage>(x => x
   .Queue("some-queue")
   .WithConsumer<TConsumer>()
   .Instances(1));
```

### Consumer context

The consumer can implement the `IConsumerWithContext` interface to access the Azure Service Bus native message:

```cs
public class PingConsumer : IConsumer<PingMessage>, IConsumerWithContext
{
   public IConsumerContext Context { get; set; }

   public Task OnHandle(PingMessage message, string path)
   {
      // Azure SB transport specific extension:
      var transportMessage = Context.GetTransportMessage(); // Of type Azure.Messaging.ServiceBus.ServiceBusReceivedMessage
   }
}
```

This could be useful to extract the message's `CorrelationId` or `ApplicationProperties` (from Azure SB native client type `Azure.Messaging.ServiceBus.ServiceBusReceivedMessage`).

> Since version 1.15.5 the Azure SB client was updated, so the native message type is now `Azure.Messaging.ServiceBus.ServiceBusReceivedMessage` (it used to be `Azure.ServiceBus.Message`).

### Exception Handling for Consumers

In case the consumer was to throw an exception while processing a message, SMB marks the message as abandoned.
This results in a message delivery retry performed by Azure SB (potentially event in another running instance of your service). By default, Azure SB retries 10 times. After last attempt the message Azure SB moves the message to a dead letter queue (DLQ). More information [here](https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-dead-letter-queues).

If you need to send only selected messages to DLQ, wrap the body of your consumer method in a `try-catch` block and rethrow the exception for only the messages you want to be moved to DLQ (after the retry limit is reached).

SMB will also set a user property `SMB.Exception` on the message with the exception details (just the message, no stack trace). This should be helpful when reviewing messages on the DLQ.

### Transport Specific Settings

> Since version 1.15.6

The consumer expose additional settings from the underlying ASB client:

- [PrefetchCount](https://docs.microsoft.com/en-us/dotnet/api/azure.messaging.servicebus.servicebusprocessoroptions.prefetchcount)
- [MaxAutoLockRenewalDuration](https://docs.microsoft.com/en-us/dotnet/api/azure.messaging.servicebus.servicebusprocessoroptions.maxautolockrenewalduration)
- [SubQueue](https://docs.microsoft.com/en-us/dotnet/api/azure.messaging.servicebus.servicebusprocessoroptions.subqueue)

```cs
mbb.Consume<TMessage>(x => x
   .WithConsumer<TConsumer>()
   .Queue("some-queue")
   .MaxAutoLockRenewalDuration(TimeSpan.FromMinutes(7))
   .SubQueue(SubQueue.DeadLetter)
   .PrefetchCount(10)
   .Instances(1));
```

Where applicable, selected settings can have the default values applied using `ServiceBusMessageBusSettings`:

```cs
mbb.WithProviderServiceBus(new ServiceBusMessageBusSettings(connectionString) {
   MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(7),
   PrefetchCount = 10,
})
```

However, the specific settings applied at the consumer level take priority.

## Request-Response Configuration

### Produce Request Messages

The request sending service needs to configure where the request message should be delivered - to SB topic or to SB queue. See the [Producing Messages](#producing-messages).

The SMB Azure Service Bus provider needs to know if you want to receive responses on a Service Bus topic:

```cs
mbb.ExpectRequestResponses(x =>
{
    x.ReplyToTopic("test-echo-resp");
    x.SubscriptionName("response-consumer");
    x.DefaultTimeout(TimeSpan.FromSeconds(60));
});
```

or a Service Bus queue:

```cs
mbb.ExpectRequestResponses(x =>
{
    x.ReplyToQueue("test-echo-queue-resp");
    x.DefaultTimeout(TimeSpan.FromSeconds(60));
});
```

In either case, each of your micro-service instances must have its own dedicated queue (or topic). At the end, when your n-th service instance sends a request, we want the response to arrive back to that n-th service instance. Specifically, this is required so that the internal `Task<TResponse>` of `bus.Send(TRequest)` is resumed and the parent task can continue.

It is preferred to receive responses on queues rather than topics.

### Handle Request Messages

The request processing service (the responding service) in the case of SMB Azure Service Bus providers needs to configure the queue (or topic) that the request messages will be consumed from:

```cs
mbb.Handle<EchoRequest, EchoResponse>(x => x
        .Topic(topic)
        .SubscriptionName("handler")
        .WithHandler<EchoRequestHandler>()
        .Instances(2));
```

```cs
mbb.Handle<EchoRequest, EchoResponse>(x => x
        .Queue(queue)
        .WithHandler<EchoRequestHandler>()
        .Instances(2));
```

When a request message is send to a queue the handler service has to also consume from that queue. Likewise, if you send a request to a topic it has to be consumed from that topic. You cannot mix sending to a topic and consuming from a queue (or vice versa).

## ASB Sessions

> Since 1.16.1

SMB has support for the [Azure Service Bus sessions](https://docs.microsoft.com/en-us/azure/service-bus-messaging/message-sessions).
Sessions allow achieving a FIFO guarantee in Service Bus.

The respective topic's subscription or queue requires sessions enabled on the Azure Service Bus side.

The producer side has to set the `SessionId` property on the native ASB message, which is achieved using the `.WithModifier()`:

```cs
mbb.Produce<CustomerMessage>(x => x
   .DefaultQueue(queue)
   .WithModifier((message, sbMessage) =>
   {
      // Use the customer id as the session id to ensure the same messages related to the same customer messages are delivered FIFO order.
      sbMessage.SessionId = message.CustomerId.ToString();
   }))
```

The consumer side has to enable sessions:

```cs
mbb.Consume<CustomerMessage>(x => x
   .Queue(queue)
   .WithConsumer<CustomerConsumer>()
   // Defines how many concurrent message processings will be done within a single ongoing session
   // To achieve FIFO, this should be 1 (the default)
   .Instances(1)
   // Enables sessions, this process can handle up to 10 sessions concurrently, each session will expire after 5 seconds of inactivity
   .EnableSession(x => x.MaxConcurrentSessions(10).SessionIdleTimeout(TimeSpan.FromSeconds(5))));
   // Alternatively just enable session on the consumer, and take the default values from ServiceBusMessageBusSettings (see below)
   //.EnableSession())
```

When the session settings inside `.EnableSessions()` are not provided, they can be provided in the bus settings. Doing so, will apply these as default values for every consumer with sessions enabled:

```cs
mbb.WithProviderServiceBus(new ServiceBusMessageBusSettings(connectionString)
{
   SessionIdleTimeout = TimeSpan.FromSeconds(5),
   MaxConcurrentSessions = 10,   
});
```

When there is a need to get ahold of the `SessionId` for the message processed, the [`IConsumerWithContext`](#consumer-context) should be helpful.

> ASB also allows storage and retrieval of session-specific data. Currently, SMB does not support that. This feature can be added if there are asks from the community.
