**SlimMessageBus** is a lightweight and extensible message bus framework for .NET, designed to simplify working with message brokers and in-process messaging.

It supports a variety of transport providers and offers built-in patterns like publish/subscribe and request/response over queues.

### Supported Transports

- Amazon SQS/SNS
- Apache Kafka
- Azure Event Hub
- Azure Service Bus
- Hybrid (combine multiple transports)
- In-memory (for domain events and mediator-style messaging)
- MQTT / Azure IoT Hub
- NATS
- RabbitMQ
- Redis
- SQL (MS SQL)

### Available Plugins

- **FluentValidation** for message validation
- **Transactional Outbox** pattern (supports SQL, PostgreSQL, and DbContext)
- **Serialization** with JSON, Avro, or ProtoBuf
- **AsyncAPI** spec generation
- **Consumer Circuit Breaker** with Health Checks integration

For full documentation and examples, visit the [GitHub repository](https://github.com/zarusz/SlimMessageBus).
