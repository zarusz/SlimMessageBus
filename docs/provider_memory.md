# Memory (in-process) Provider for SlimMessageBus <!-- omit in toc -->

Please read the [Introduction](intro.md) before reading this provider documentation.

- [Introduction](#introduction)
- [Configuration](#configuration)
  - [Disabling serialization](#disabling-serialization)
  - [Virtual Topics](#virtual-topics)
  - [Autoregistration](#autoregistration)
- [Lifecycle](#lifecycle)
  - [Blocking Publish](#blocking-publish)
  - [Per-Message DI scope](#per-message-di-scope)
  
## Introduction

The Memory transport provider can be used for internal communication within the same process. It is the simplest transport provider and does not require any external messaging infrastructure.

> Since messages are passed in memory and never persisted, they will be lost if your app dies while consuming these messages.

A fitting use case for in memory communication is to integrate your domain layer with other application layers via domain events.

> The request-response has been implemented with completion of issue #7.

## Configuration

The memory transport is configured using the `.WithProviderMemory()`:

```cs
// MessageBusBuilder mbb;
mbb.    
   .WithProviderMemory(new MemoryMessageBusSettings())
   .WithSerializer(new JsonMessageSerializer());
```

### Disabling serialization

Since messages are passed within the same process, serializing and deserializing them might be redundant or a desired performance optimization. Serialization can be disabled:

```cs
// MessageBusBuilder mbb;
mbb.    
   .WithProviderMemory(new MemoryMessageBusSettings
   {
      // Do not serialize the domain events and rather pass the same instance across handlers (faster 
      EnableMessageSerialization = false
   });
   //.WithSerializer(new JsonMessageSerializer()); // no serializer  needed
```

> When serialization is disabled for in memory passed messages, the exact same object instance send by the producer will be recieved by the consumer. Therefore state changes on the consumer end will be visible by the producer. Consider making the messages immutable (read only).

### Virtual Topics

Unlike other transport providers, memory transport does not have true notion of topics (or queues). However, it is still required to use topic names. This is required, so that the bus knows on which virtual topic to deliver the message to and from what virtual topic to consume.

Here is an example for the producer side:

```cs
// declare that OrderSubmittedEvent will be produced
mbb.Produce<OrderSubmittedEvent>(x => x.DefaultTopic(x.Settings.MessageType.Name));

// alternatively
mbb.Produce<OrderSubmittedEvent>(x => x.DefaultTopic("OrderSubmittedEvent"));
```

and the consumer side:

```cs
// declare that OrderSubmittedEvent will be consumed
mbb.Consume<OrderSubmittedEvent>(x => x.Topic(x.MessageType.Name).WithConsumer<OrderSubmittedHandler>());

// alternatively
mbb.Consume<OrderSubmittedEvent>(x => x.Topic("OrderSubmittedEvent").WithConsumer<OrderSubmittedHandler>());
```

The benefit is that we can channel messages of the same type via different virtual topics.

### Autoregistration

During configuration, we can discover all the handlers (or messages that will be produced) and register them in the bus. This can be useful to autoregister all of your domain handlers from the application layer. 

It required a bit of reflection code and the `.Do(Action<MessageBusBuilder> action)` method:

```cs
Assembly applicationLayer = ...

// auto discover consumers/handler (classes that implement IConsumer<T>) in the applicationLayer assembly and register with the bus
mbb.Do(builder => applicationLayer.GetTypes().Where(t => t.IsClass && !t.IsAbstract)
                    .SelectMany(t => t.GetInterfaces(), (t, i) => new { Type = t, Interface = i })
                    .Where(x => x.Interface.IsGenericType && x.Interface.GetGenericTypeDefinition() == typeof(IConsumer<>))
                    .Select(x => new { HandlerType = x.Type, EventType = x.Interface.GetGenericArguments()[0] })
                    .ToList()
                    .ForEach(find =>
                    {
                        Log.InfoFormat(CultureInfo.InvariantCulture, "Registering {0} in the bus", find.EventType);
                        builder.Produce(find.EventType, x => x.DefaultTopic(x.Settings.MessageType.Name));
                        builder.Consume(find.EventType, x => x.Topic(x.MessageType.Name).WithConsumer(find.HandlerType));
                    })
                );
```

## Lifecycle

### Blocking Publish

The `Send<T>()` is blocking and `Publsh<T>()` is blocking by default. 
It might be expected that the `Publish<T>()` to be fire-and-forget and non-blocking, however this is to ensure the consumer/handler have been processed by the time the method call returns. That behavior is optimized for domain-events where you expect the side effects to be executed synchronously.

ToDo: In the future we will want to expose a setting to make the `Publish<T>()` non-blocking.

### Per-Message DI scope

> Unlike stated for the [Introduction](intro.md) the memory bus has per-message scoped disabled by default.
