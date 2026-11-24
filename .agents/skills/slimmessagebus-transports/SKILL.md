---
name: slimmessagebus-transports
description: Configure SlimMessageBus transports and endpoint routing. Use when adding or changing a transport provider, configuring queues, topics, subscriptions, or groups, or composing multiple providers with a hybrid bus.
---

# Configure SlimMessageBus transports

1. Identify the requested provider and the app's existing package versions. Read the relevant [provider reference](references/providers.md) and check the provider API against the version in use. Use the provider package and its builder extension namespace in the startup project.
2. Set the provider in the existing `AddSlimMessageBus(...)` registration. Bind connections and credentials from the application's configuration or identity provider. For brokered messages, verify that each participant uses a compatible serializer and message contract.
3. Map the producer destination and receiving endpoint according to the provider's topology. Check the queue or subscription for each receiver, the consumer group where applicable, and the provider's reply mechanism for `Send`. Keep existing destination names and delivery settings when extending an application.
4. Check how the topology is provisioned. Use existing entities when the deployment owns them. When the app owns provisioning, use the provider's supported settings and verify the identity has the required permissions.

For one transport, configure it on the bus. For multiple transports in one process, use named `AddChildBus(...)` registrations and put each producer, consumer, and request/reply declaration on the relevant child. Keep shared services and serializers on the root where appropriate. A publish can target multiple child buses that declare the message type. A request must resolve to one child bus. [Hybrid provider](https://github.com/zarusz/SlimMessageBus/blob/master/docs/provider_hybrid.md)

Build the host. When a local or test broker is available, verify startup, routing, and any reply path on that transport. A Memory smoke test can verify application wiring and handler logic. Broker topology, credentials, and delivery behavior require the configured provider.
