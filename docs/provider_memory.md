# Memory (in-process) Provider for SlimMessageBus <!-- omit in toc -->

Please read the [Introduction](intro.md) before reading this provider documentation.

- [Introduction](#introduction)
- [Configuration](#configuration)
  - [Serialization](#serialization)
  - [Virtual Topics](#virtual-topics)
  - [Auto Declaration](#auto-declaration)
    - [Polymorphic message support](#polymorphic-message-support)
- [Lifecycle](#lifecycle)
  - [Blocking Publish](#blocking-publish)
  - [Error Handling](#error-handling)
  - [Per-Message DI scope](#per-message-di-scope)
- [Benchmarks](#benchmarks)
  
## Introduction

The Memory transport provider can be used for internal communication within the same process. It is the simplest transport provider and does not require any external messaging infrastructure.

> Since messages are passed in memory and never persisted, they will be lost if the application process terminates while consuming these messages.

Good use case for in memory communication is:

- to integrate the domain layer with other application layers via domain events pattern,
- to implement mediator pattern (when combined with [interceptors](intro.md#interceptors)),
- to run unit tests against application code that normally runs with an out of process transport provider (Kafka, Azure Service Bus, etc).

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

Unlike other transport providers, memory transport does not have true notion of topics (or queues). However, it is still required to use topic names. This is required, so that the bus knows on which virtual topic to deliver the message to, and from what virtual top
erSubmittedEvent"));

and the consumer side:

```cs
// declare that OrderSubmittedEvent will be consumed
mbb.Consume<OrderSubmittedEvent>(x => x.Topic(x.MessageType.Name).WithConsumer<OrderSubmittedHandler>());

// alternatively
mbb.Consume<OrderSubmittedEvent>(x => x.Topic("OrderSubmittedEvent").WithConsumer<OrderSubmittedHandler>());
```

> The virtual topic name can be any string. It helps to connect the relevant producers and consumers together.

### Auto Declaration

> Since 1.19.1

For bus configuration, we can leverage `AutoDeclareFrom()` method to discover all the consumers (`IConsumer<T>`) and handlers (`IRequestHandler<T,R>`) types and auto declare the respective producers and consumers/handlers in the bus.
This can be useful to auto declare all of the domain event handlers in an application layer.

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
   public Task<EchoResponse> OnHandle(EchoRequest request) { /* ... */ }
}
```

The bus registrations will end up:

```cs
mbb.Produce<EchoRequest>(x => x.DefaultTopic(x.MessageType.Name));
mbb.Handle<EchoRequest, EchoResponse>(x => x.Topic(x.MessageType.Name).WithConsumer<EchoRequestHandler>());
```

The vitual topic name will be derived from the message type name by default. This can be customized by passing an additional parameter to the `AutoDeclareFrom()` method.

Using `AutoDeclareFrom()` to configure the memory bus is recommended, as it provides a good developer experience.

> Note that it is still required to register (or auto register) the consumer/handler types in the underlying DI (see [here](intro.md#autoregistration-of-consumers-interceptors-and-configurators)).

#### Polymorphic message support

> Since 1.21.0

The polymorphic message types (message that share a common ancestry) are supported by the `AutoDeclareFrom()`:

- for every consumer / handler implementation found it analyzes the message type inheritance tree,
- it declares a producer in the bus for the oldest ancestor of the message type hierarchy,
- it declares a consumer in the bus for the oldest ancestor message type and within that, configures a consumer for derived message type,
- topic names are derived from the ancestor message type.

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

- The test includes a 1M of simple messages being produced.
- The consumers do not do any logic - we want to test how fast can the messages flow through the bus.
- The benchmark application uses a real life setup including dependency injection container.
- There is a viaration of the test that captures the overhead for the interceptor pipeline.

Pub/Sub scenario results:

| Method                        | messageCount |    Mean |    Error |   StdDev |        Gen0 |      Gen1 |      Gen2 | Allocated |
| ----------------------------- | ------------ | ------: | -------: | -------: | ----------: | --------: | --------: | --------: |
| PubSub                        | 1000000      | 1.201 s | 0.0582 s | 0.0816 s | 118000.0000 | 3000.0000 | 3000.0000 | 489.03 MB |
| PubSubWithConsumerInterceptor | 1000000      | 1.541 s | 0.0247 s | 0.0219 s | 191000.0000 | 3000.0000 | 3000.0000 | 778.95 MB |
| PubSubWithProducerInterceptor | 1000000      | 1.479 s | 0.0094 s | 0.0078 s | 200000.0000 | 3000.0000 | 3000.0000 | 817.09 MB |
| PubSubWithPublishInterceptor  | 1000000      | 1.511 s | 0.0178 s | 0.0219 s | 200000.0000 | 3000.0000 | 3000.0000 | 817.09 MB |

> Pub/Sub rate is 832639 messages/s on the tested machine (without interceptors).

Request/Response scenario results:

| Method                               | messageCount |    Mean |    Error |   StdDev |        Gen0 |       Gen1 |      Gen2 |  Allocated |
| ------------------------------------ | ------------ | ------: | -------: | -------: | ----------: | ---------: | --------: | ---------: |
| RequestResponse                      | 1000000      | 2.424 s | 0.0351 s | 0.0274 s | 134000.0000 | 39000.0000 | 6000.0000 |  801.83 MB |
| ReqRespWithConsumerInterceptor       | 1000000      | 3.056 s | 0.0608 s | 0.0769 s | 205000.0000 | 55000.0000 | 6000.0000 | 1229.08 MB |
| ReqRespWithProducerInterceptor       | 1000000      | 2.957 s | 0.0517 s | 0.0458 s | 229000.0000 | 60000.0000 | 6000.0000 | 1374.04 MB |
| ReqRespWithRequestHandlerInterceptor | 1000000      | 3.422 s | 0.0644 s | 0.0742 s | 217000.0000 | 58000.0000 | 6000.0000 | 1297.74 MB |
| ReqRespWithSendInterceptor           | 1000000      | 2.934 s | 0.0285 s | 0.0223 s | 219000.0000 | 59000.0000 | 7000.0000 | 1305.38 MB |

> Request/Response rate is 412541 messages/s on the tested machine (without interceptors).

The test results are for the following environment:

```text
BenchmarkDotNet=v0.13.2, OS=Windows 11 (10.0.22621.819)
Intel Core i7-8550U CPU 1.80GHz (Kaby Lake R), 1 CPU, 8 logical and 4 physical cores
.NET SDK=6.0.402
  [Host]     : .NET 6.0.11 (6.0.1122.52304), X64 RyuJIT AVX2
  Job-XKUBHP : .NET 6.0.11 (6.0.1122.52304), X64 RyuJIT AVX2
```

See the benchmark source [here](../src/Tests/SlimMessageBus.Host.Memory.Benchmark).
