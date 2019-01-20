# Azure ServiceBus notes for SlimMessageBus

## Configuration

Azure Service Bus supports topics (that enable pub/sub communicaton) and queues. SMB needs to know wheather you want to produce (and consume) a message to a topic or a queue. This is defined as part of the `MessageBusBuilder` configuration:

```cs
var connectionString = // Azure Service Bus connection string

MessageBusBuilder mbb = MessageBusBuilder
    .Create()
    // your bus configuration here:
    // mbb.
    .WithProviderServiceBus(new ServiceBusMessageBusSettings(connectionString))
    .WithSerializer(new JsonMessageSerializer())

IMessageBus bus = mbb.Build();
```

## Producing messages

To produce a given `TMessage` to a Azure Serivce Bus queue (or topic) use:

```cs
mbb
    // send TMessage to Azure SB queues
    .Produce<TMessage>(x => x.UseQueue()) 
    // send TMessage to Azure SB topics
    //.Produce<TMessage>(x => x.UseTopic())  
```

This tells the runtime that all messages of type `TMessage` should go into a Azure Service Bus queue (or topic). Then anytime you publish a message like this:

```cs
TMessage msg = // ...
bus.Publish(msg, "some-queue") // msg will go to the "some-queue" queue
//bus.Publish(msg, "some-topic") // msg will go to the "some-topic" topic
```

It will be interpreted as either a queue name or topic name depending on the bus configuration.

When you se the default queue (or topic) for a message:

```cs
mbb
    .Produce<TMessage>(x => x.DefaultQueue("some-queue"))    
    //.Produce<TMessage>(x => x.DefaultTopic("some-topic"))
```

And skip the name, then the default queue (or topic) name is going to be used:

```cs
bus.Publish(msg) // msg will go to the "some-queue" queue (or "some-topic")
```

Setting the default queue name `DefaultQueue(string)` for a message type will implicitly configure `UseQueue()` for that message type. By default if no configuration is present the runtime assumes a message needs to sent on a topic (and works as if `UseTopic()` was configured).


## Consuming messages

To consume `TMessage` by `TConsumer` from `some-topic` Azure SB topic use:

```cs
mbb.SubscribeTo<TMessage>(x => x
    .Topic("some-topic")
    .SubscriptionName($"subscriber-name") // ensure subscription exists on the ServiceBus topic
    .WithSubscriber<TConsumer>()
    .Instances(1));
```

Notice the subscription name needs to be provided when consuming from a topic.

To consume `TMessage` by `TConsumer` from `some-queue` Azure SB queue use:

```cs
mbb.SubscribeTo<TMessage>(x => x
    .Queue("some-queue")
    .WithSubscriber<TConsumer>()
    .Instances(1));
```

## Request-response communication

ToDo
