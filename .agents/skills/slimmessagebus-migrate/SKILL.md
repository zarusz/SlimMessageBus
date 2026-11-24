---
name: slimmessagebus-migrate
description: Migrate a .NET application from MediatR or MassTransit to SlimMessageBus. Use to map handlers, middleware, message contracts, and broker endpoints while preserving behavior during the transition.
---

# Migrate to SlimMessageBus

Inventory the message types, call sites, handlers, middleware, and delivery guarantees in use. Keep the existing behavior visible in focused tests. Migrate an endpoint or feature at a time when the application needs staged rollout. Confirm APIs against the installed SlimMessageBus version.

## From MediatR

- Keep in-process dispatch on the Memory transport. Map request senders to `IRequestResponseBus.Send`, requests to `IRequest<TResponse>` or `IRequest`, and handlers to `IRequestHandler` implementations with `OnHandle`. Map notifications to `IPublishBus.Publish` and `IConsumer<T>`.
- Choose explicit declarations or Memory's `AutoDeclareFromAssemblyContaining<T>()` to fit the app. Check notification fan-out, exception propagation, cancellation, and whether publish completion depends on handlers finishing.
- Map pipeline behaviors to the matching producer, send, consumer, or request-handler interceptors. Check validation and transaction placement and preserve the DI scope used by handlers and their dependencies.

## From MassTransit

- Map each existing publish, explicit send-to-endpoint, and request-client call separately. In SlimMessageBus, `Send` is request/reply. For a one-way delivery to a specific destination, use the selected transport's queue or path configuration with `Publish`.
- Translate consumers to `IConsumer<T>.OnHandle` or request handlers to `IRequestHandler`. Preserve or map endpoint names, subscriptions, bindings, concurrency, retry, and error handling. Verify the destination and the provider-specific reply topology for requests.
- For mixed deployments or queued messages from the previous bus, verify actual payloads, envelopes, headers, type identifiers, and correlation. Even when both apps use JSON, check how MassTransit and SlimMessageBus format the wire message. Arrange a bridge, drain, or coordinated cutover when the formats or topology differ.

Verify the migrated handlers using the original behavioral expectations. For brokered endpoints, exercise routing and failure behavior with the real provider before cutover.

[MediatR migration overview](https://github.com/zarusz/SlimMessageBus/blob/master/docs/UseCases/ReplaceMediatR.md) · [MassTransit migration overview](https://github.com/zarusz/SlimMessageBus/blob/master/docs/UseCases/ReplaceMassTransit.md) · [Current configuration examples](https://github.com/zarusz/SlimMessageBus/blob/master/docs/intro.md#configuration)
