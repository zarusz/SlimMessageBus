---
name: slimmessagebus-pipelines
description: Add or change SlimMessageBus pipeline behavior. Use for producer or consumer validation, publish and send interceptors, request-handler middleware, consumer error handlers, or retry and dead-letter policies.
---

# Extend SlimMessageBus pipelines

- Choose the hook by operation. `IProducerInterceptor<T>` wraps both publish and send. Use `IPublishInterceptor<T>` or `ISendInterceptor<TRequest, TResponse>` for one producer operation, and `IConsumerInterceptor<T>` or `IRequestHandlerInterceptor<TRequest, TResponse>` for incoming work. For a request without a response, check the documentation for the `Void` response type used by its interceptor.
- Register the implemented interfaces in DI through the application's existing assembly scan or explicit registrations. Use `IInterceptorWithOrder` where order matters, and check how the installed version composes generic and operation-specific interceptors. Invoke and await `next` for normal continuation. A deliberate short-circuit skips the later pipeline and the operation it wraps.
- For FluentValidation, register `IValidator<T>` in DI and configure `AddFluentValidation(...)` with `AddProducerValidatorsFromAssemblyContaining<T>()` or `AddConsumerValidatorsFromAssemblyContaining<T>()`. Choose the stage based on where the invariant needs to hold, including messages from external publishers.
- For processing failures, choose `IConsumerErrorHandler<T>` or the transport's specialized handler. A registered provider-specific handler takes precedence. Check what the provider does for failure, success, in-process retry, and any requeue or dead-letter outcome it supports. Bound retries and account for delivery attempts already made by the broker.
- Check the per-message scope when interceptors set up a transaction or contextual service that a handler will use. Inspect the order and scope before changing lifetimes.

Verify hook order and registration through the bus entry point. Exercise normal continuation, expected short-circuit or validation failure, and the configured error outcome. Use the actual provider when asserting acknowledgements, broker redelivery, or dead-letter behavior.

[Interceptors](https://github.com/zarusz/SlimMessageBus/blob/master/docs/intro.md#interceptors) · [FluentValidation](https://github.com/zarusz/SlimMessageBus/blob/master/docs/plugin_fluent_validation.md) · [Consumer error handling](https://github.com/zarusz/SlimMessageBus/blob/master/docs/intro.md#error-handling)
