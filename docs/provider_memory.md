# Memory (in-process) Provider for SlimMessageBus <!-- omit in toc -->

Please read the [Introduction](intro.md) before reading this provider documentation.

- [Introduction](#introduction)
- [Configuration](#configuration)
  - [Disabling serialization](#disabling-serialization)
  - [Virtual Topics](#virtual-topics)
  - [Auto Declaration](#auto-declaration)
- [Lifecycle](#lifecycle)
  - [Blocking Publish](#blocking-publish)
  - [Per-Message DI scope](#per-message-di-scope)
  
## Introduction

The Memory transport provider can be used for internal communication within the same process. It is the simplest transport provider and does not require any external messaging infrastructure.

> Since messages are passed in memory and never persisted, they will be lost if your app dies while consuming these messages.

Good use case for in memory communication is to integrate the domain layer with other application layers via domain events pattern.

## Configuration

The memory transport is configured using the `.WithProviderMemory()`:

```cs
// MessageBusBuilder mbb;
mbb
   .WithProviderMemory(new MemoryMessageBusSettings())
   .WithSerializer(new JsonMessageSerializer());
```

### Disabling serialization

Since messages are passed within the same process, serializing and deserializing them might be redundant or a desired performance optimization. Serialization can be disabled:

```cs
// MessageBusBuilder mbb;
mbb
   .WithProviderMemory(new MemoryMessageBusSettings
   {
      // Do not serialize the domain events and rather pass the same instance across handlers (faster 
      EnableMessageSerialization = false
   });
   //.WithSerializer(new JsonMessageSerializer()); // no serializer  needed
```

> When serialization is disabled for in memory passed messages, the exact same object instance send by the producer will be recieved by the consumer. Therefore state changes on the consumer end will be visible by the producer. Consider making the messages immutable (read only).

### Virtual Topics

Unlike other transport providers, memory transport does not have true notion of topics (or queues). However, it is still required to use topic names. This is required, so that the bus knows on which virtual topic to deliver the message to, and from what virtual topic to consume.

Here is an example for the producer side:

```cs
// declare that OrderSubmittedEvent will be produced
mbb.Produce<OrderSubmittedEvent>(x => x.DefaultTopic(x.MessageType.Name));

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

### Auto Declaration

> Since 1.19.1

During bus configuration, we can leverage `AutoDeclareFromConsumers()` method to discover all the consumers (`IConsumer<T>`) and handlers (`IRequestHandler<T,R>`) types and auto declare the respective producers and consumers/handlers in the bus. This can be useful to auto declare all of the domain event handlers in an application layer.

```cs
mbb
   .WithProviderMemory(new MemoryMessageBusSettings())
   .AutoDeclareFromConsumers(Assembly.GetExecutingAssembly());
   // If we want to filter to specific consumer/handler types then we can supply an additional filter:
   //.AutoDeclareFromConsumers(Assembly.GetExecutingAssembly(), consumerTypeFilter: (consumerType) => consumerType.Name.EndsWith("Handler"));
```

For example, assuming this is the discovered type:

```cs
public class EchoRequestHandler : IRequestHandler<EchoRequest, EchoResponse>
{
   public Task<EchoResponse> OnHandle(EchoRequest request, string name) { /* ... */ }
}
```

The bus registrations will end up:

```cs
mbb.Produce<EchoRequest>(x => x.DefaultTopic(x.MessageType.Name));
mbb.Handle<EchoRequest, EchoResponse>(x => x.Topic(x.MessageType.Name).WithConsumer<EchoRequestHandler>());
```

> Using `AutoDeclareFromConsumers` to configure the memory bus is recommended, as it provides a good developer experience.

## Lifecycle

### Blocking Publish

The `Send<T>()` is blocking and `Publsh<T>()` is blocking by default.
It might be expected that the `Publish<T>()` to be fire-and-forget and non-blocking, however this is to ensure the consumer/handler have been processed by the time the method call returns. That behavior is optimized for domain-events where you expect the side effects to be executed synchronously.

ToDo: In the future we will want to expose a setting to make the `Publish<T>()` non-blocking.

> In contrast to MediatR, having the `Publish<T>` blocking, allows us to avoid having to use `Send<T, R>` and have the developer to use `Unit` or `VoidResult`.

### Per-Message DI scope

> Unlike stated for the [Introduction](intro.md) the memory bus has per-message scoped disabled by default. However, the memory bus consumer/handler would join the already ongoing DI scope. This is desired in scenarios where an external message is being handled as part of the unit of work (Kafka/ASB, etc) or an API HTTP request is being handled - the memory bus consumer/handler will be looked up in the ongoing/current DI scope.
