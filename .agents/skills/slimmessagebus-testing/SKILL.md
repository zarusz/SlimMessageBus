---
name: slimmessagebus-testing
description: Test a SlimMessageBus application. Use to verify handlers, real DI and bus wiring, concurrent message delivery, or routing and failure behavior with a test broker.
---

# Test SlimMessageBus applications

Choose the test level based on the behavior under test.

| Behavior | Test approach |
| --- | --- |
| Handler logic | Invoke `OnHandle` directly with its real or substituted dependencies and assert its result or side effect. |
| DI registration, dispatch, or request/reply | Build and start a test host with Memory. Reuse the application's registration where practical, resolve the bus from the intended scope, and exercise the real `Publish` or `Send` entry point. |
| Payload and header compatibility | Use wire fixtures with the production serializer and type resolver. Memory serialization is disabled by default. Its serialization opt-in can exercise round trips while a provider test verifies the actual wire path. |
| Broker routing, acknowledgements, redelivery, or reply topology | Use the actual provider with an isolated local broker, Testcontainers fixture, emulator where supported, or existing test infrastructure. Match the production queue, subscription, binding, or group semantics. |

For asynchronous consumption, signal completion from the handler or observe its durable side effect with a bounded wait. Awaiting `Publish` may complete before consumption, including on Memory with asynchronous publish enabled. Make failures identify the message and endpoint. Isolate parallel tests with unique names, and shut down hosts before cleaning up test entities.

Run the smallest relevant test set first. When the broker is unavailable, report which broker-specific expectations still need verification.

[Memory lifecycle](https://github.com/zarusz/SlimMessageBus/blob/master/docs/provider_memory.md#lifecycle) · [SlimMessageBus repository test setup](https://github.com/zarusz/SlimMessageBus/blob/master/docs/Maintainers/build.md)
