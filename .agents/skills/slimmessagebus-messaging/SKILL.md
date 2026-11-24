---
name: slimmessagebus-messaging
description: Add SlimMessageBus producers, consumers, and request handlers to a .NET application. Use for message contracts, publish and send calls, endpoint declarations, DI registration, and request replies.
---

# Add SlimMessageBus messaging

Find the host or module that owns the endpoint and check its SlimMessageBus package versions. Reuse its message contracts, bus configuration, and path naming. In a multi-transport application, put each declaration on the child bus that owns its endpoint.

## Publish and consume

- Define or reuse the message contract in the appropriate shared or application project. Declare `Produce<TMessage>(x => x.DefaultPath(path))` on the publishing side. Inject `IPublishBus` or `IMessageBus` and call `await bus.Publish(message, cancellationToken: cancellationToken)`.
- On the receiving side, implement `IConsumer<TMessage>.OnHandle(TMessage, CancellationToken)` and declare `Consume<TMessage>(x => x.Path(path))`. Verify that the publish destination reaches the consumer through the transport's queue, subscription, or binding. Producer and consumer paths have different roles on some transports.
- Treat bus declarations and DI registration as distinct. `AddServicesFromAssemblyContaining<TConsumer>()` registers discovered concrete types and interfaces with DI. With manual registration, register `IConsumer<TMessage>` when relying on the default, or the concrete type when using `.WithConsumer<TConsumer>()`. Use the equivalent interface or concrete registration for request handlers. Apply the project's service lifetime conventions.
- For Memory, check whether the app already uses `AutoDeclareFromAssemblyContaining<T>()`. That workflow can declare routes and register the discovered services.

## Send and handle requests

- For a typed response, implement `IRequest<TResponse>` on the request and `IRequestHandler<TRequest, TResponse>` on the handler. Declare `Produce<TRequest>(x => x.DefaultPath(path))` on the caller and `Handle<TRequest, TResponse>(x => x.Path(path))` on the receiver. Inject `IRequestResponseBus` or `IMessageBus` and await `bus.Send(request, cancellationToken: cancellationToken)`.
- For a request that only needs completion or error feedback, use `IRequest`, `IRequestHandler<TRequest>`, and `Handle<TRequest>(...)`. `Send` still waits for the handler. Use `Publish` for a message with no awaited handler response.
- For brokered requests, use the provider's reply configuration. Providers using `ExpectRequestResponses(...)` generally need a dedicated reply destination per requesting instance and any required reply subscription, group, or binding. Memory provides default in-process request/response settings.

Use constructor-injected `IConsumerContext` if a consumer needs headers or transport context and the installed version supports it. Keep the application's per-message scope settings, and verify that background consumers can resolve scoped dependencies from the intended scope.

Build the changed projects. Verify delivery on the intended path or, for request/response, that the caller receives the expected reply or error. Test broker routing with the configured transport when available. [Pub/sub](https://github.com/zarusz/SlimMessageBus/blob/master/docs/intro.md#pubsub-communication) · [Request/response](https://github.com/zarusz/SlimMessageBus/blob/master/docs/intro.md#request-response-communication)
