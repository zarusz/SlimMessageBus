# AGENTS.md

This repository is SlimMessageBus, a .NET message bus library.

GitHub repository: https://github.com/zarusz/SlimMessageBus

## Project Layers

- `src/SlimMessageBus` contains the core public interfaces and contracts, such as `IMessageBus`, `IPublishBus`, `IRequestResponseBus`, `IConsumer<TMessage>`, `IRequestHandler<TRequest, TResponse>`, and request marker types.
- `src/SlimMessageBus.Host` contains the main hosting/runtime implementation used with .NET Generic Host and Microsoft dependency injection. It wires bus configuration, producers, consumers, request/response, message scopes, serialization hooks, and lifecycle control.
- `src/SlimMessageBus.Host.Configuration`, `src/SlimMessageBus.Host.Interceptor`, and related `SlimMessageBus.Host.*` projects add host-level configuration, interceptors, validation, circuit breakers, outbox support, serialization, and other extension points.
- Transport and feature plugins live in `src/SlimMessageBus.Host.*`, for example `SlimMessageBus.Host.Kafka`, `SlimMessageBus.Host.AzureServiceBus`, `SlimMessageBus.Host.RabbitMQ`, `SlimMessageBus.Host.AmazonSQS`, `SlimMessageBus.Host.Memory`, `SlimMessageBus.Host.Redis`, `SlimMessageBus.Host.Nats`, `SlimMessageBus.Host.Mqtt`, and `SlimMessageBus.Host.Sql`.
- `src/Samples` contains sample applications and usage examples.
- Tests live under `src/Tests`, usually matching the package or plugin being tested.

## Minimal Coding Context

- Applications configure the bus via `services.AddSlimMessageBus(mbb => { ... })`.
- `MessageBusBuilder` declarations describe produced and consumed message types, paths such as topics or queues, transport provider settings, request/response settings, serializers, and additional plugins.
- Producers use `IMessageBus` / `IPublishBus` to publish messages and `IRequestResponseBus` for request/response.
- Consumers implement `IConsumer<TMessage>` for pub/sub or queue consumption.
- Request handlers implement `IRequestHandler<TRequest, TResponse>` or `IRequestHandler<TRequest>`.
- The host runtime resolves consumers, handlers, interceptors, serializers, and other services from Microsoft DI.
- Per-message DI scopes, consumer context, message headers, filters, interceptors, error handlers, and topology provisioning are cross-cutting host concepts.
- Transport plugins can add provider-specific builders, settings, context operations, topology behavior, and error handling. Prefer keeping provider-specific behavior inside the relevant `SlimMessageBus.Host.*` plugin.

## Development Notes

- Follow existing patterns in the package/plugin being changed.
- Keep core interfaces in `SlimMessageBus` transport-agnostic.
- Keep common host behavior in `SlimMessageBus.Host` or shared host extension projects.
- Keep provider-specific code, options, and tests in the relevant `SlimMessageBus.Host.*` project.
- When changing behavior, add or update focused tests under the matching `src/Tests/...` project.
- Add or update documentation under `docs` for relevant plugin or core functionality changes. Prefer evolving the corresponding `*.t.md` template first, then run `./build/md-processor.ps1` to regenerate the published markdown.
- Ask for, or add, respective unit tests for changed functionality. The test stack uses xUnit, Moq, and FluentAssertions.
