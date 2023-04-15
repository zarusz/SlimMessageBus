# SlimMessageBus

SlimMessageBus is a client façade for message brokers for .NET.
It comes with implementations for specific brokers and in-memory message passing (in-process communication).
SlimMessageBus additionally provides request-response implementation over message queues, and many other plugins.

Transports:

- Apache Kafka
- Azure Service Bus
- Azure Event Hub
- Redis
- MQTT / Azure IoT Hub
- In-Memory transport (domain events, mediator)
- Hybrid (composition of the bus out of many transports)

Plugins:

- Message validation via Fluent Validation
- Transactional Outbox pattern
- Serialization using JSON, Avro, ProtoBuf
- AsyncAPI specification generation

Find out more [https://github.com/zarusz/SlimMessageBus](https://github.com/zarusz/SlimMessageBus).
