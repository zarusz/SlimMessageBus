---
name: slimmessagebus-contracts
description: Evolve SlimMessageBus message contracts and serialization across independently deployed applications. Use for serializer changes, DTO or schema evolution, MessageType resolution, polymorphism, or interoperability with external producers.
---

# Evolve message contracts

1. Identify producers and consumers that share the endpoint, their deployed versions, and any retained or queued messages that must still be readable. Capture representative payloads and headers in a test fixture, including the existing `MessageType` value where used.
2. Check type resolution separately from payload deserialization. The default resolver uses an assembly-qualified CLR name without assembly version information. A type, namespace, or assembly rename can still change that identifier. Use the documented resolver redirect or a compatible custom resolver if consumers need to recognize old names.
3. Keep serializer settings and schema rules compatible with deployed applications. Verify field names, optional or required fields, defaults, and polymorphic discriminators in the selected serializer. For Avro or Protobuf, check the schema or field identifiers as part of the change. If several serializers are configured, check which one handles the message in both applications.
4. For external producers, verify the actual wire format and headers. When `MessageType` is absent, use a receiving path with one declared message type or arrange for a recognized identifier to be supplied. For polymorphic messages, check both the bus's derived-type dispatch and the serializer's derived-type support.

Test old producer payloads against new consumers. When new producers and old consumers will overlap, test the reverse direction too. Include old queued or outbox messages and representative derived types where applicable. If the versions cannot overlap safely, plan a compatible transition or a new endpoint.

[Serialization plugins](https://github.com/zarusz/SlimMessageBus/blob/master/docs/serialization.md) · [Message type resolution and redirects](https://github.com/zarusz/SlimMessageBus/blob/master/docs/intro.md#message-type-resolver) · [Polymorphic messages](https://github.com/zarusz/SlimMessageBus/blob/master/docs/intro.md#polymorphic-messages)
