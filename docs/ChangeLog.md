# SlimMessageBus changelog

Notable changes in the 3.x series, newest first. See [GitHub Releases](https://github.com/zarusz/SlimMessageBus/releases) for detailed notes and earlier versions.

## Unreleased

- Deferred consumer and request handler resolution until the interceptor pipeline reaches the invocation. Short-circuited dispatches skip resolution even with manual consumer disposal enabled. `IConsumerContext.Consumer` returns `null` until resolution.
- Added nine public [Agent Skills](agent-skills.md) for setting up, extending, migrating, testing, and troubleshooting SlimMessageBus applications.
- Made critical Trivy vulnerabilities and misconfigurations fail the GitHub Actions build.
- Published Trivy security findings directly in the GitHub Actions build run summary.
- Fixed consumer context isolation for concurrent and nested Memory transport sends that share a DI scope, including legacy `IConsumerWithContext` handlers. Dispatch from the root provider works with scope validation when the handler's dependencies can be resolved from the root. Dependencies that inject the concrete `ConsumerContext` must switch to `IConsumerContext`. [#506](https://github.com/zarusz/SlimMessageBus/issues/506) · [#410](https://github.com/zarusz/SlimMessageBus/issues/410)
- Reduced allocations during dispatch with the built-in message scope. [#507](https://github.com/zarusz/SlimMessageBus/pull/507)

## [3.6.2](https://github.com/zarusz/SlimMessageBus/releases/tag/3.6.2) · 2026-11-07 · Prerelease

- Separated the idle polling delay from the immediate polling window for SQL Server and PostgreSQL transports.
- Updated transport, outbox, and serialization dependencies, including security fixes, and aligned .NET 10 package versions.

## [3.6.1](https://github.com/zarusz/SlimMessageBus/releases/tag/3.6.1) · 2026-11-02 · Prerelease

- Fixed SQL Server migration lock handling when a lock is acquired after waiting.

## [3.6.0](https://github.com/zarusz/SlimMessageBus/releases/tag/3.6.0) · 2026-08-14

- Added the Google Cloud Pub/Sub transport.
- Added MQTT topic wildcard support and optimized subscription matching.
- Improved SQL Server outbox reconnect and migration recovery, and documented managed identity setup.
- Fixed RabbitMQ request-response handling when a response fails to deserialize.

## [3.5.0](https://github.com/zarusz/SlimMessageBus/releases/tag/3.5.0) · 2026-07-09

- Added SQL Server and PostgreSQL transports and a MongoDB transactional outbox.
- Added Kafka consumer loop failure interception.
- Improved SQS message deletion and fixed SNS topic lookup when topology provisioning is disabled.
- Fixed retries after a failed outbox batch.

## [3.4.0](https://github.com/zarusz/SlimMessageBus/releases/tag/3.4.0) · 2025-12-06

- Added .NET 10 support and consumer filtering by message headers.
- Upgraded to RabbitMQ.Client 7 and MQTTnet 5. Added RabbitMQ publisher confirms and NATS queue groups.
- Fixed RabbitMQ handling of unmatched routing keys and System.Text.Json handling of runtime message types.

## [3.3.6](https://github.com/zarusz/SlimMessageBus/releases/tag/3.3.6) · 2025-11-28

- Fixed publish and send to pass the declared producer type for derived messages.

## [3.3.5](https://github.com/zarusz/SlimMessageBus/releases/tag/3.3.5) · 2025-10-22

- Fixed a semaphore leak that could stall consumers after processing failures.
- Improved RabbitMQ recovery after a server restart and fixed SQS handling of messages without attributes.

## [3.3.4](https://github.com/zarusz/SlimMessageBus/releases/tag/3.3.4) · 2025-10-03

- Upgraded the Amazon SQS transport to AWS SDK v4.

## [3.3.3](https://github.com/zarusz/SlimMessageBus/releases/tag/3.3.3) · 2025-09-19

- Added RabbitMQ routing key wildcard support.

## [3.3.2](https://github.com/zarusz/SlimMessageBus/releases/tag/3.3.2) · 2025-09-01

- Improved RabbitMQ connection resilience, validation, and default exchange handling.
- Fixed child buses ignoring the consumer auto-start setting.

## [3.3.1](https://github.com/zarusz/SlimMessageBus/releases/tag/3.3.1) · 2025-07-28

- Fixed Redis list consumption after a disconnection.
- Added dynamic Azure Service Bus topology creation for overridden publish paths and fixed batch splitting for Azure Service Bus and Event Hubs.

## [3.3.0](https://github.com/zarusz/SlimMessageBus/releases/tag/3.3.0) · 2025-06-22

- Added Amazon SNS support to the SQS transport.
- Exposed the full transport message to deserializers and propagated deserialization failures to the transport.

## [3.2.0](https://github.com/zarusz/SlimMessageBus/releases/tag/3.2.0) · 2025-04-18

- Added a PostgreSQL transactional outbox and constructor injection of `IConsumerContext`.
- Added redirects for renamed message types and assemblies. Memory auto-declaration now uses fully qualified message type names by default.
- Added support for RabbitMQ exchange types specified by name, including delayed-message exchanges.

## [3.1.1](https://github.com/zarusz/SlimMessageBus/releases/tag/3.1.1) · 2025-04-05

- Protected SQL Server outbox migrations with a distributed lock.
- Added DI service registration to Memory transport auto-declaration.

## [3.1.0](https://github.com/zarusz/SlimMessageBus/releases/tag/3.1.0) · 2025-03-04

- Added per-path serializers and batched outbox cleanup.
- Exposed the Azure Service Bus subscription name through the consumer context and adopted .NET's `TimeProvider`.

## [3.0.2](https://github.com/zarusz/SlimMessageBus/releases/tag/3.0.2) · 2025-02-16

- Enabled parallel Azure Service Bus subscription provisioning.

## [3.0.1](https://github.com/zarusz/SlimMessageBus/releases/tag/3.0.1) · 2025-01-30 · Prerelease

- Added Azure Service Bus error handling options for modifying message properties and supplying a dead-letter reason and description.

## [3.0.0](https://github.com/zarusz/SlimMessageBus/releases/tag/3.0.0) · 2025-01-15

- Added the Amazon SQS transport, batch publishing, a health-check-based consumer circuit breaker, and .NET 9 support.
- Expanded consumer retry and error handling and added cancellation tokens to consumer and request-handler interfaces. See the release's migration guide for breaking changes.
- Improved the SQL Server outbox, expanded serializer support for text payloads, and added Azure Service Bus correlation filters.
