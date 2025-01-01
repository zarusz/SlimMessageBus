# SlimMessageBus

SlimMessageBus is a client façade for message brokers for .NET.
It comes with implementations for specific brokers and in-memory message passing (in-process communication).
SlimMessageBus additionally provides request-response implementation over message queues, and many other plugins.

Transports:

- Amazon SQS/SNS
- Apache Kafka
- Azure Event Hub
- Azure Service Bus
- Hybrid (composition of the bus out of many transports)
- In-Memory transport (domain events, mediator)
- MQTT / Azure IoT Hub
- NATS
- RabbitMQ
- Redis
- SQL (MS SQL, PostgreSql)

Plugins:

- Message validation via Fluent Validation
- Transactional Outbox pattern (SQL, DbContext)
- Serialization using JSON, Avro, ProtoBuf
- AsyncAPI specification generation
- Consumer Circuit Breaker based on Health Checks

Find out more [https://github.com/zarusz/SlimMessageBus](https://github.com/zarusz/SlimMessageBus).
