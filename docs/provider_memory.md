# Memory (in-process) Provider for SlimMessageBus <!-- omit in toc -->

Please read the [Introduction](intro.md) before reading this provider documentation.

- [Introduction](#introduction)
- [Configuration](#configuration)
  - [Serialization](#serialization)
  - [Virtual Topics](#virtual-topics)
  - [Auto Declaration](#auto-declaration)
- [Lifecycle](#lifecycle)
  - [Blocking Publish](#blocking-publish)
  - [Error Handling](#error-handling)
  - [Per-Message DI scope](#per-message-di-scope)
- [Benchmarks](#benchmarks)
  
## Introduction

The Memory transport provider can be used for internal communication within the same process. It is the simplest transport provider and does not require any external messaging infrastructure.

> Since messages are passed in memory and never persisted, they will be lost if the application process dies while consuming these messages.

Good use case for in memory communication is to integrate the domain layer with other application layers via domain events pattern, or for mediator pattern (when combined with [interceptors](intro.md#interceptors)).

## Configuration

The memory transport is configured using the `.WithProviderMemory()`:

```cs
// MessageBusBuilder mbb;
mbb
   .WithProviderMemory();
```

### Serialization

Since messages are passed within the same process, serializing and deserializing them is redundant. Also disabling serialization gives a performance optimization.

> Serialization is disabled by default for memory bus.

Serialization can be disabled or enabled:

```cs
// MessageBusBuilder mbb;
mbb
   .WithProviderMemory(new MemoryMessageBusSettings
   {
      // Do not serialize the domain events and rather pass the same instance across handlers
      EnableMessageSerialization = false
   });
   //.WithSerializer(new JsonMessageSerializer()); //  serializer not needed if EnableMessageSerialization = false
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

### Auto Declaration

> Since 1.19.1

For bus configuration, we can leverage `AutoDeclareFrom()` method to discover all the consumers (`IConsumer<T>`) and handlers (`IRequestHandler<T,R>`) types and auto declare the respective producers and consumers/handlers in the bus. This can be useful to auto declare all of the domain event handlers in an application layer.

```cs
mbb
   .WithProviderMemory()
   .AutoDeclareFrom(Assembly.GetExecutingAssembly());
   // If we want to filter to specific consumer/handler types then we can supply an additional filter:
   //.AutoDeclareFrom(Assembly.GetExecutingAssembly(), consumerTypeFilter: (consumerType) => consumerType.Name.EndsWith("Handler"));
```

For example, assuming this is the discovered type:

```cs
public class EchoRequestHandler : IRequestHandler<EchoRequest, EchoResponse>
{
   public Task<EchoResponse> OnHandle(EchoRequest request, string path) { /* ... */ }
}
```

The bus registrations will end up:

```cs
mbb.Produce<EchoRequest>(x => x.DefaultTopic(x.MessageType.Name));
mbb.Handle<EchoRequest, EchoResponse>(x => x.Topic(x.MessageType.Name).WithConsumer<EchoRequestHandler>());
```

Using `AutoDeclareFrom` to configure the memory bus is recommended, as it provides a good developer experience.

> Note that it is still required to register (or auto register) the consumer/handler types in the underlying DI (see [here](intro.md#autoregistration-of-consumers-interceptors-and-configurators)).

## Lifecycle

### Blocking Publish

The `Send<T>()` is blocking and `Publsh<T>()` is blocking by default.
It might be expected that the `Publish<T>()` to be fire-and-forget and non-blocking, however this is to ensure the consumer/handler have been processed by the time the method call returns. That behavior is optimized for domain-events where you expect the side effects to be executed synchronously.

ToDo: In the future we will want to expose a setting to make the `Publish<T>()` non-blocking.

> In contrast to MediatR, having the `Publish<T>` blocking, allows us to avoid having to use `Send<T, R>` and have the developer to use `Unit` or `VoidResult`.

### Error Handling

The exceptions raised in an handler (`IRequestHandler<T, R>`) are bubbled up to the sender (`await bus.Send(request)`).
Likewise, the exceptions raised in an consumer (`IConsumer<T>`) are also bubbled up to the publisher (`await bus.Publish<T>(message)`).

The [interceptors](intro.md#interceptors) are able to handle the error before it reaches back to the publisher or sender.

### Per-Message DI scope

Unlike stated for the [Introduction](intro.md) the memory bus has per-message scoped disabled by default. However, the memory bus consumer/handler would join the already ongoing DI scope. This is desired in scenarios where an external message is being handled as part of the unit of work (Kafka/ASB, etc) or an API HTTP request is being handled - the memory bus consumer/handler will be looked up in the ongoing/current DI scope.

## Benchmarks

The project [`SlimMessageBus.Host.Memory.Benchmark`](/src/Tests/SlimMessageBus.Host.Memory.Benchmark/) runs basic scenarios for pub/sub and request/response on the memory bus. The benchmark should provide some reference about the speed / performance of the Memory Bus.

- The test includes a simple messages being produced.
- The consumers do not do any logic - we want to test how fast can the messages flow through the bus.
- The benchmark application uses a real life setup including dependency injection container.
- No interceptors are being used.

Pub/Sub scenario results:

| Method | messageCount |           Mean |        Error |       StdDev |       Gen 0 |     Gen 1 |     Gen 2 |  Allocated |
| ------ | -----------: | -------------: | -----------: | -----------: | ----------: | --------: | --------: | ---------: |
| PubSub |          100 |       114.7 us |      1.75 us |      1.36 us |     13.3057 |         - |         - |      55 KB |
| PubSub |         1000 |     1,181.5 us |      4.57 us |      3.81 us |    130.8594 |         - |         - |     540 KB |
| PubSub |        10000 |    11,891.2 us |    116.28 us |    108.77 us |   1296.8750 |   62.5000 |   31.2500 |   5,491 KB |
| PubSub |       100000 |   117,676.6 us |  1,641.94 us |  1,455.54 us |  12800.0000 |  600.0000 |  600.0000 |  54,394 KB |
| PubSub |      1000000 | 1,204,072.3 us | 17,743.36 us | 18,221.12 us | 128000.0000 | 3000.0000 | 3000.0000 | 539,825 KB |

> Pub/Sub rate is 830515 messages/s on the tested machine.

Request/Response scenario results:

| Method          | messageCount |           Mean |        Error |       StdDev |       Gen 0 |      Gen 1 |     Gen 2 |    Allocated |
| --------------- | -----------: | -------------: | -----------: | -----------: | ----------: | ---------: | --------: | -----------: |
| RequestResponse |          100 |       215.6 us |      2.78 us |      2.46 us |     28.0762 |     0.2441 |         - |       115 KB |
| RequestResponse |         1000 |     2,119.3 us |     30.73 us |     25.66 us |    277.3438 |    35.1563 |         - |     1,141 KB |
| RequestResponse |        10000 |    24,601.0 us |    221.66 us |    185.10 us |   2000.0000 |   968.7500 |  468.7500 |    11,507 KB |
| RequestResponse |       100000 |   278,643.3 us |  5,465.46 us |  8,509.06 us |  19000.0000 |  6000.0000 | 2000.0000 |   114,559 KB |
| RequestResponse |      1000000 | 2,753,348.9 us | 25,449.35 us | 23,805.34 us | 186000.0000 | 51000.0000 | 6000.0000 | 1,141,392 KB |

> Request/Response rate is 363194 messages/s on the tested machine.

The test results are for the following environment:

```text
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.22000
Intel Core i7-8550U CPU 1.80GHz (Kaby Lake R), 1 CPU, 8 logical and 4 physical cores
.NET SDK=6.0.302
```
