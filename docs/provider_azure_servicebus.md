# Azure Service Bus Provider for SlimMessageBus

## Introduction

Please read the [Introduction](intro.md) before reading this provider documentation.

## Configuration

Azure Service Bus provider requires a connection string:

```cs
var connectionString = "" // Azure Service Bus connection string

MessageBusBuilder mbb = MessageBusBuilder
    .Create()
    // your bus configuration here
    .WithProviderServiceBus(new ServiceBusMessageBusSettings(connectionString))
    .WithSerializer(new JsonMessageSerializer());

IMessageBus bus = mbb.Build();
```

The `ServiceBusMessageBusSettings` has additional settings that allow to override factories for Azure SB client objects. This may be used for some advanced scenarios.

Since Azure SB supports topics and queues, the SMB needs to know wheather you want to produce (consume) message to (from) a topic or a queue. This is defined as part of the `MessageBusBuilder` configuration.

### Producing Messages

To produce a given `TMessage` to a Azure Serivce Bus queue (or topic) use:

```cs
// send TMessage to Azure SB queues
mbb.Produce<TMessage>(x => x.UseQueue()); 

// send TMessage to Azure SB topics
mbb.Produce<TMessage>(x => x.UseTopic());
```

This configures the runtime that all messages of type `TMessage` should be delivered to a Azure Service Bus queue (topic). 

Then anytime you produce a message like this:

```cs
TMessage msg;

// msg will go to the "some-queue" queue
bus.Publish(msg, "some-queue");

// OR

// msg will go to the "some-topic" topic
bus.Publish(msg, "some-topic");
```

The second (name) parameter will be interpreted as either a queue name or topic name - depending on the bus configuration.

If you configure the default queue (or default topic) for a message type:

```cs
mbb.Produce<TMessage>(x => x.DefaultQueue("some-queue"));    
// OR
mbb.Produce<TMessage>(x => x.DefaultTopic("some-topic"));
```

and skip the second (name) parameter in `Publish()`, then that default queue (or default topic) name is going to be used:

```cs
// msg will go to the "some-queue" queue (or "some-topic")
bus.Publish(msg);
```

Setting the default queue name `DefaultQueue()` for a message type will implicitly configure `UseQueue()` for that message type. By default if no configuration is present the runtime will assume a message needs to be sent on a topic (and works as if `UseTopic()` was configured).


### Consuming Messages

To consume `TMessage` by `TConsumer` from `some-topic` Azure Service Bus topic use:

```cs
mbb.SubscribeTo<TMessage>(x => x
    .Topic("some-topic")
    .SubscriptionName($"subscriber-name")
    .WithSubscriber<TConsumer>()
    .Instances(1));
```

Notice the subscription name needs to be provided when consuming from a topic. Furthermore, the subscription has to be created in Azue before running your application (it is not automatically created).

To consume `TMessage` by `TConsumer` from `some-queue` Azure Service Bus queue use:

```cs
mbb.SubscribeTo<TMessage>(x => x
    .Queue("some-queue")
    .WithSubscriber<TConsumer>()
    .Instances(1));
```

### Request-Response Configuration

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

In either case it is important that each of your micro-service instances have their own dedicated queue (or topic). In the end, when your n-th service instance sends a request, we want the response to arrive back to that n-th service instance. Specifically, this is required so that the internal `Task<TResponse>` of `bus.Send(TRequest)` is resumed and the parent task can continue.

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