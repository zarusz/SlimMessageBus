---
name: slimmessagebus-troubleshoot
description: Diagnose a SlimMessageBus failure in an existing application. Use when the bus fails to start, messages do not arrive, a consumer fails, deserialization breaks, or Send times out.
---

# Troubleshoot SlimMessageBus

Start with the failing message type, bus or child bus, transport, installed package versions, and producer and consumer logs for the same attempt. Identify the last stage with evidence of success before changing configuration. In a test environment, reproduce with one representative message and a known destination.

| Symptom | Checks |
| --- | --- |
| Host or consumers fail to start | Read the first startup exception. Check DI resolution, the transport connection, entity provisioning and permissions, and whether the host starts consumers or uses `AutoStartConsumersEnabled(false)` with explicit startup. |
| Publish succeeds but the consumer never runs | Trace the actual destination from the caller's `path` argument or the producer's default to the provider's queue, topic, group, subscription, or binding. Check the child bus and the `Consume` declaration. Check the DI registration expected by the declaration, and any header filter or competing consumer. |
| A message arrives but cannot be dispatched | Inspect the `MessageType` header and type resolution before investigating payload deserialization. A message without that header can be inferred only if the receiving path has one declared message type. If the type resolves, compare the serializer, payload format, and contract versions. |
| `Send` times out | Follow the request to the handler and then follow the response back to the originator. Check the handler exception or result, request correlation, reply destination, and provider-specific reply routing for the calling instance. Increase the timeout only if the observed processing time justifies it. |
| A message is retried or dead-lettered | Inspect the original exception and delivery attempts. Distinguish an in-process retry from transport redelivery, and check the selected error handler and provider-specific disposition. |

Use the smallest change supported by the evidence. Verify the original scenario using the same transport when available, and report the failing stage, the evidence, and any remaining unverified hop.

[Logging and error handling](https://github.com/zarusz/SlimMessageBus/blob/master/docs/intro.md#error-handling) · [Request/reply routing](https://github.com/zarusz/SlimMessageBus/blob/master/docs/intro.md#dedicated-reply-queuetopic) · [Provider documentation](https://github.com/zarusz/SlimMessageBus/blob/master/docs/README.md)
