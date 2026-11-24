# Agent Skills

Use these [Agent Skills](https://agentskills.io/specification) to guide changes in a .NET application that uses SlimMessageBus.

## Use the skills in your project

Copy the complete skill folders you want into the target project's `.agents/skills/` for Codex or `.claude/skills/` for Claude Code. Then ask your agent to use the skill by name. [Codex skills](https://learn.chatgpt.com/docs/build-skills#where-codex-loads-local-skills) · [Claude Code skills](https://code.claude.com/docs/en/skills#choose-where-skills-load)

## Set up messaging

### [`slimmessagebus-setup`](../.agents/skills/slimmessagebus-setup/SKILL.md)

Use when adding SlimMessageBus to an ASP.NET Core, Worker, or Generic Host application. The skill covers package selection for your target framework, host registration, serializer setup, and a runnable Memory smoke test.

> Use slimmessagebus-setup to add SlimMessageBus to this worker and verify that an in-process event reaches its consumer.

### [`slimmessagebus-messaging`](../.agents/skills/slimmessagebus-messaging/SKILL.md)

Use when adding a producer, consumer, or request handler to an existing bus. It guides the message contract, bus declarations, DI registration, and reply configuration for requests.

> Use slimmessagebus-messaging to publish OrderPlaced and add a consumer in the fulfillment worker.

### [`slimmessagebus-transports`](../.agents/skills/slimmessagebus-transports/SKILL.md)

Use when configuring a provider or combining transports. Its provider reference maps packages and builder methods to routing concepts such as Kafka groups, RabbitMQ bindings, and Azure Service Bus subscriptions.

> Use slimmessagebus-transports to add an Azure Service Bus child bus for integration events and keep domain events on the Memory bus.

## Extend or migrate an application

### [`slimmessagebus-outbox`](../.agents/skills/slimmessagebus-outbox/SKILL.md)

Use when a database change and an outgoing event must be persisted in the same transaction. The skill covers provider integration, opting producers into the outbox, and testing rollback and eventual delivery.

> Use slimmessagebus-outbox to publish OrderPlaced through the PostgreSQL EF Core outbox and verify that a rolled-back order never produces an event.

### [`slimmessagebus-migrate`](../.agents/skills/slimmessagebus-migrate/SKILL.md)

Use when moving from MediatR or MassTransit. It helps map handlers and middleware, inventory existing behavior, and check broker topology and wire compatibility before migrating an endpoint.

> Use slimmessagebus-migrate to move this MediatR command to Memory while preserving its validation and exception behavior.

### [`slimmessagebus-contracts`](../.agents/skills/slimmessagebus-contracts/SKILL.md)

Use when changing a message type, schema, serializer, or type identifier across deployments. It helps verify that compatible producers and consumers can overlap and that queued messages remain readable.

> Use slimmessagebus-contracts to rename OrderPlaced's namespace while keeping messages from older publishers readable.

### [`slimmessagebus-pipelines`](../.agents/skills/slimmessagebus-pipelines/SKILL.md)

Use when adding validation, interceptors, or consumer error handling. The skill helps choose the stage, register the hook, check execution order and DI scope, and verify the provider's retry or dead-letter behavior.

> Use slimmessagebus-pipelines to validate incoming orders before the handler writes to the database.

## Verify and troubleshoot

### [`slimmessagebus-testing`](../.agents/skills/slimmessagebus-testing/SKILL.md)

Use when deciding what to test directly, through a real Memory bus, or against a test broker. It covers waiting for asynchronous consumption and isolating test endpoints.

> Use slimmessagebus-testing to verify handler wiring with Memory and RabbitMQ redelivery with a test broker.

### [`slimmessagebus-troubleshoot`](../.agents/skills/slimmessagebus-troubleshoot/SKILL.md)

Use when a configured bus fails to start, deliver, deserialize, or return a reply. The skill traces the failure from the last confirmed stage and uses provider-specific checks to narrow down the cause.

> Use slimmessagebus-troubleshoot to find why Send times out even though the handler logs a successful result.
