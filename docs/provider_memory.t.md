# Memory (in-process) Provider for SlimMessageBus <!-- omit in toc -->

Please read the [Introduction](intro.md) before reading this provider documentation.

- [Introduction](#introduction)
- [Configuration](#configuration)
  - [Virtual Topics](#virtual-topics)
  - [Auto Declaration](#auto-declaration)
    - [Polymorphic Message Support](#polymorphic-message-support)
  - [Serialization](#serialization)
  - [Headers](#headers)
- [Lifecycle](#lifecycle)
  - [Concurrency and Ordering](#concurrency-and-ordering)
  - [Blocking Publish](#blocking-publish)
  - [Asynchronous Publish](#asynchronous-publish)
  - [Error Handling](#error-handling)
  - [Per-Message DI scope](#per-message-di-scope)
- [Benchmarks](#benchmarks)

## Introduction

The **Memory transport provider** enables message-based communication within a single process. It’s the simplest transport option in SlimMessageBus and doesn’t require any external infrastructure like Kafka or Azure Service Bus.

> ⚠️ Messages are passed entirely in memory and are never persisted. If the application process terminates while messages are in-flight, those messages will be lost.

**Common use cases for in-memory transport include:**

- Integrating the domain layer with other application layers using the **domain events** pattern.
- Implementing the **mediator pattern** (especially when combined with [interceptors](intro.md#interceptors)).
- Writing **unit tests** for messaging logic without requiring a full transport setup.
- Starting with messaging quickly and easily — no infrastructure required — and switching to an external provider later by reconfiguring SlimMessageBus.

## Configuration

First, install the NuGet package:

```bash
dotnet add package SlimMessageBus.Host.Memory
```

Then, configure the memory transport using `.WithProviderMemory()`:

```csharp
using SlimMessageBus.Host.Memory;

services.AddSlimMessageBus(mbb =>
{
    // Configure your bus here...
    mbb.WithProviderMemory(); // Requires SlimMessageBus.Host.Memory package
});
```

> 💡 No serializer is needed by default for the in-memory transport — unless you [opt in to serialization](#serialization).

### Virtual Topics

The in-memory transport doesn't use real topics or queues like other transport providers. However, **virtual topic names** are still required. These names allow the bus to correctly route messages to and from the appropriate consumers.

On the **consumer side**, use `.Topic()` to specify the virtual topic:

```csharp
// Register a consumer for OrderSubmittedEvent
mbb.Consume<OrderSubmittedEvent>(x => x.Topic(x.MessageType.Name).WithConsumer<OrderSubmittedHandler>());

// Or use a hardcoded topic name
mbb.Consume<OrderSubmittedEvent>(x => x.Topic("OrderSubmittedEvent").WithConsumer<OrderSubmittedHandler>());
```

On the **producer side**, use `.DefaultTopic()` to define where messages should be published:

```csharp
mbb.Produce<OrderSubmittedEvent>(x => x.DefaultTopic("OrderSubmittedEvent"));
```

> Virtual topic names can be any string, as long as producers and consumers use the same value. This is what links them together internally.

### Auto Declaration

You can simplify your bus configuration using the `.AutoDeclareFrom()` method, which scans an assembly to automatically discover all consumers (`IConsumer<T>`) and handlers (`IRequestHandler<T,R>`). It then declares the corresponding producers and consumers/handlers on the bus for you.  
This is especially useful for automatically registering all domain event handlers within an application layer.

Example usage:

```csharp
mbb
   .WithProviderMemory()
   .AutoDeclareFrom(Assembly.GetExecutingAssembly());

// Alternatively, specify a type from the assembly you want to scan:
// .AutoDeclareFromAssemblyContaining<CustomerCreateHandler>();

// You can also filter which consumers/handlers are picked up:
// .AutoDeclareFrom(Assembly.GetExecutingAssembly(), consumerTypeFilter: consumerType => consumerType.Name.EndsWith("Handler"));
// .AutoDeclareFromAssemblyContaining<CustomerCreateHandler>(consumerTypeFilter: consumerType => consumerType.Name.EndsWith("Handler"));
```

For example, given a handler like this:

```csharp
public class EchoRequestHandler : IRequestHandler<EchoRequest, EchoResponse>
{
   public Task<EchoResponse> OnHandle(EchoRequest request, CancellationToken cancellationToken)
   {
       // ...
   }
}
```

The bus will automatically configure registrations equivalent to:

```csharp
mbb.Produce<EchoRequest>(x => x.DefaultTopic(x.MessageType.FullName));
mbb.Handle<EchoRequest, EchoResponse>(x => x.Topic(x.MessageType.FullName).WithConsumer<EchoRequestHandler>());
```

By default, the virtual topic name is derived from the message type’s [`FullName`](https://learn.microsoft.com/en-us/dotnet/api/system.type.fullname?view=net-9.0) — meaning it includes both the namespace and any outer class names.  
You can customize this by providing your own `messageTypeToTopicConverter`:

```csharp
services.AddSlimMessageBus(builder =>
    builder.WithProviderMemory()
           .AutoDeclareFrom(Assembly.GetExecutingAssembly(), messageTypeToTopicConverter: messageType => messageType.FullName)
);
```

Using `.AutoDeclareFrom()` is highly recommended when configuring the memory bus, as it ensures producers and consumers are kept up to date automatically as your application evolves.

> Note: `.AutoDeclareFrom()` and `.AutoDeclareFromAssemblyContaining<T>()` will also register discovered consumers and handlers into the Microsoft Dependency Injection (MSDI) container. [Learn more here](intro.md#autoregistration-of-consumers-interceptors-and-configurators).

#### Polymorphic Message Support

The memory transport supports **polymorphic message types**—messages that share a common base class or interface—via the `AutoDeclareFrom()` method.

Here’s how it works:

- For each discovered consumer or handler, the message type's inheritance hierarchy is analyzed.
- A producer is registered for the **root ancestor** of the message type.
- A consumer is registered for the **root ancestor**, and configured to handle the specific **derived** message type.
- The topic name is based on the **ancestor message type**, ensuring consistent routing for all related messages.

This allows you to declare base-level contracts and consume polymorphic messages seamlessly.

### Serialization

Because messages are exchanged entirely within the same process, serialization is typically unnecessary—and skipping it can yield performance benefits.

> 🟢 **Serialization is disabled by default** for the memory transport.

However, you can opt-in to serialization if needed—for example, to simulate production scenarios during testing or to enforce immutability:

```csharp
services.AddSlimMessageBus(mbb =>
{
    mbb.WithProviderMemory(cfg =>
    {
        // Enable serialization for in-memory messages
        cfg.EnableMessageSerialization = true;
    });

    // Required only if serialization is enabled
    mbb.AddJsonSerializer();
});
```

> ⚠️ When serialization is disabled, the **same object instance** sent by the producer is received by the consumer.  
> This means any changes made by the consumer will be visible to the producer. To avoid side effects, it’s recommended to use **immutable message types** in such cases.

### Headers

The headers published to the memory bus are delivered to the consumer.
This is managed by the `EnableMessageHeaders` setting, which is enabled by default.

```cs
services.AddSlimMessageBus(mbb =>
{
   mbb.WithProviderMemory(cfg =>
   {
      // Header passing can be disabled when not needed (and to save on memory allocations)
      cfg.EnableMessageHeaders = false;
   });
});
```

Before version v2.5.1, to enable header passing the [serialization](#serialization) had to be enabled.

## Lifecycle

### Concurrency and Ordering

The order of message delivery to consumer will match the order of producer.

By default, each consumer processes one message at a time to ensure ordering.
To [increase the concurrency level](intro.md#concurrently-processed-messages) use the `.Instances(n)` setting:

```cs
mbb.Consume<PingMessage>(x => x.Instances(2));
```

When number of concurrent consumer instances > 0 (`.Instances(N)`) then up to N messages will be processed concurrently (having impact on ordering).

### Blocking Publish

Similar to `Send<T>()`, the `Publish<T>()` is blocking by default.

It might be expected that the `Publish<T>()` to be non-blocking (asynchronous) and that the consumer processes the message in the background.
However, blocking mode is enabled by default to ensure the consumer has finished processing by the time the method call returns.
Often we want all the side effect to finish within the unit of work (ongoing web-request, or external message being handled).
That behavior is optimized for domain-events where the side effects are to be executed synchronously within the unit of work.

### Asynchronous Publish

To use non-blocking publish use the `EnableBlockingPublish` property setting:

@[:cs](../src/Tests/SlimMessageBus.Host.Memory.Test/MemoryMessageBusIt.cs,ExampleSetup)

When the `.Publish<T>()` is invoked in the non-blocking mode:

- the consumers will be executed in another async task (in thMemoryMessageBusIte background),
- that task cancellation token will be bound to the message bus lifecycle (consumers are stopped, the bus is disposed or application shuts down),
- the order of message delivery to consumer will match the order of publish,
- however, when number of concurrent consumer instances > 0 (`.Instances(N)`) then up to N messages will be processed concurrently (having impact on ordering)
- the unit of work where the message is published is decoupled from the consumer unit of work, there will be an independent per message scope created in the DI on the consumer side - this allows to scope consumer dependencies to the message unit of work.

### Error Handling

The exceptions raised in a handler (`IRequestHandler<T, R>`) are bubbled up to the sender (`await bus.Send(request)`).
Likewise, the exceptions raised in a consumer (`IConsumer<T>`) are also bubbled up to the publisher (`await bus.Publish<T>(message)`) when [blocking publish mode](#blocking-publish) is enabled.

In the case of [non-blocking publish mode](#asynchronous-publish) when an exception is raised by a message consumer, the memory transport will log the exception and move on to the next message. For more elaborate behavior a custom [error handler](intro.md#error-handling) should be setup.

There is a memory transport specific error handler interface [IMemoryConsumerErrorHandler<T>](../src/SlimMessageBus.Host.Memory/Consumers/IMemoryConsumerErrorHandler.cs) that will be preferred by the memory transport.

The error handler has to be registered in MSDI for all (or specified) message types:

```cs
// Register error handler in MSDI for any message type
services.AddTransient(typeof(IMemoryConsumerErrorHandler<>), typeof(CustomMemoryConsumerErrorHandler<>));
```

See also the common [error handling](intro.md#error-handling).

### Per-Message DI scope

Unlike stated for the [Introduction](intro.md) the memory bus has per-message scoped disabled by default.
However, the memory bus consumer/handler would join (enlist in) the already ongoing DI scope.
This is desired in scenarios where an external message is being handled as part of the unit of work (Kafka/ASB, etc) or an API HTTP request is being handled - the memory bus consumer/handler will be looked up in the ongoing/current DI scope.

## Benchmarks

The project [`SlimMessageBus.Host.Memory.Benchmark`](/src/Tests/SlimMessageBus.Host.Memory.Benchmark/) runs basic scenarios for pub/sub and request/response on the memory bus. The benchmark should provide some reference about the speed / performance of the Memory Bus.

- The test includes a 1M of simple messages being produced.
- The consumers do not do any logic - we want to test how fast can the messages flow through the bus.
- The benchmark application uses a real life setup including dependency injection container.
- There is a variation of the test that captures the overhead for the interceptor pipeline.

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
