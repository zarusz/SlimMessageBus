# SlimMessageBus <!-- omit in toc -->

SlimMessageBus is a lightweight, flexible, and extensible messaging framework for .NET, supporting multiple message brokers, including Kafka, RabbitMQ, Azure EventHubs, MQTT, Redis Pub/Sub, and more. It simplifies asynchronous communication and integrates seamlessly with modern .NET applications.

[![GitHub license](https://img.shields.io/github/license/zarusz/SlimMessageBus)](https://github.com/zarusz/SlimMessageBus/blob/master/LICENSE)
[![Build](https://github.com/zarusz/SlimMessageBus/actions/workflows/build.yml/badge.svg?branch=master)](https://github.com/zarusz/SlimMessageBus/actions/workflows/build.yml)
[![Maintainability Rating](https://sonarcloud.io/api/project_badges/measure?project=zarusz_SlimMessageBus&metric=sqale_rating)](https://sonarcloud.io/summary/overall?id=zarusz_SlimMessageBus)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=zarusz_SlimMessageBus&metric=coverage)](https://sonarcloud.io/summary/overall?id=zarusz_SlimMessageBus)
[![Duplicated Lines (%)](https://sonarcloud.io/api/project_badges/measure?project=zarusz_SlimMessageBus&metric=duplicated_lines_density)](https://sonarcloud.io/summary/overall?id=zarusz_SlimMessageBus)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=zarusz_SlimMessageBus&metric=vulnerabilities)](https://sonarcloud.io/summary/overall?id=zarusz_SlimMessageBus)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=zarusz_SlimMessageBus&metric=alert_status)](https://sonarcloud.io/summary/overall?id=zarusz_SlimMessageBus)

> See how to migrate from [MediatR](/docs/UseCases/ReplaceMediatR.md) or [MassTransit](/docs/UseCases/ReplaceMassTransit.md)

## 🚀 Quick Start

### Installation

```bash
dotnet add package SlimMessageBus
# Add specific transport provider, e.g. Kafka
dotnet add package SlimMessageBus.Host.Kafka
# Add serialization plugin
dotnet add package SlimMessageBus.Host.Serialization.SystemTextJson
```

### Basic Usage

#### Publishing Messages

```csharp
IMessageBus bus; // injected

public record OrderCreatedEvent(int OrderId);

await bus.Publish(new OrderCreatedEvent(123));
```

#### Consuming Messages

```csharp
public class OrderCreatedEventConsumer : IConsumer<OrderCreatedEvent>
{
    public async Task OnHandle(OrderCreatedEvent message, CancellationToken cancellationToken)
    {
        // Handle the event
    }
}
```

### Request-Response Example

#### Sending a Request

```csharp
public record CreateCustomerCommand(string Name) : IRequest<CreateCustomerCommandResult>;
public record CreateCustomerCommandResult(Guid CustomerId);

var result = await bus.Send(new CreateCustomerCommand("John Doe"));
```

#### Handling a Request

```csharp

public class CreateCustomerCommandHandler : IRequestHandler<CreateCustomerCommand, CreateCustomerCommandResult>
{
    public async Task<CreateCustomerCommandResult> OnHandle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        // Create customer logic
        return new(Guid.NewGuid());
    }
}
```

### Configuration Example

```csharp
services.AddSlimMessageBus(mbb =>
{
    mbb.AddChildBus("Bus1", builder =>
    {
         builder
               // the pub-sub events
               .Produce<OrderCreatedEvent>(x => x.DefaultPath("orders-topic"))
               .Consume<OrderCreatedEvent>(x => x.Path("orders-topic")
                  //.WithConsumer<OrderCreatedEventConsumer>() // Optional: can be skipped as IConsumer<OrderCreatedEvent> will be resolved from DI
                  //.KafkaGroup("kafka-consumer-group") // Kafka: Consumer Group
                  //.SubscriptionName("azure-sb-topic-subscription") // Azure ServiceBus: Subscription Name
               )

               // the request-response
               .Produce<CreateCustomerCommand>(x => x.DefaultPath("customer-requests"))
               .Handle<CreateCustomerCommand, CreateCustomerCommandResult>(x => x.Path("customer-requests"))

               // Use Kafka transport provider (requires SlimMessageBus.Host.Kafka package)
               .WithProviderKafka(cfg => cfg.BrokerList = "localhost:9092");

            // Use Azure Service Bus transport provider
            //.WithProviderServiceBus(cfg => { ... }) // requires SlimMessageBus.Host.AzureServiceBus package
            // Use Azure Event Hub transport provider
            //.WithProviderEventHub(cfg => { ... }) // requires SlimMessageBus.Host.AzureEventHub package
            // Use Redis transport provider
            //.WithProviderRedis(cfg => { ... }) // requires SlimMessageBus.Host.Redis package
            // Use RabbitMQ transport provider
            //.WithProviderRabbitMQ(cfg => { ... }) // requires SlimMessageBus.Host.RabbitMQ package

            // Use in-memory transport provider
            //.WithProviderMemory(cfg => { ... }) // requires SlimMessageBus.Host.Memory package
   })
   // Add other bus transports (as child bus for in memory domain events), if needed
   //.AddChildBus("Bus2", (builder) => {  })
   .AddJsonSerializer() // requires SlimMessageBus.Host.Serialization.SystemTextJson or SlimMessageBus.Host.Serialization.Json package
   .AddServicesFromAssemblyContaining<OrderCreatedEventConsumer>();
});
```

The configuration can be [modularized](docs/intro.md#modularization-of-configuration) (for modular monoliths).

## 📖 Documentation

- [Introduction](docs/intro.md)
- Transports:
  - [Amazon SQS/SNS](docs/provider_amazon_sqs.md)
  - [Apache Kafka](docs/provider_kafka.md)
  - [Azure EventHubs](docs/provider_azure_eventhubs.md)
  - [Azure ServiceBus](docs/provider_azure_servicebus.md)
  - [Hybrid](docs/provider_hybrid.md)
  - [MQTT](docs/provider_mqtt.md)
  - [Memory](docs/provider_memory.md)
  - [NATS](docs/provider_nats.md)
  - [RabbitMQ](docs/provider_rabbitmq.md)
  - [Redis](docs/provider_redis.md)
  - [SQL](docs/provider_sql.md)
- Plugins:
  - [Serialization](docs/serialization.md)
  - [Transactional Outbox](docs/plugin_outbox.md)
  - [Validation using FluentValidation](docs/plugin_fluent_validation.md)
  - [AsyncAPI specification generation](docs/plugin_asyncapi.md)
  - [Consumer Circuit Breaker](docs/intro.md#health-check-circuit-breaker)
- [Samples](src/Samples/README.md)
- [Use Cases](docs/UseCases/)

## 📦 NuGet Packages

| Name                                 | Description                                                                                                         | NuGet                                                                                                                                                                            |
| ------------------------------------ | ------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `SlimMessageBus`                     | The core API for SlimMessageBus                                                                                     | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.svg)](https://www.nuget.org/packages/SlimMessageBus)                                                                     |
| **Transports**                       |                                                                                                                     |                                                                                                                                                                                  |
| `.Host.AmazonSQS`                    | Transport provider for Amazon SQS / SNS                                                                             | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.AmazonSQS.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.AmazonSQS)                                       |
| `.Host.AzureEventHub`                | Transport provider for Azure Event Hubs                                                                             | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.AzureEventHub.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.AzureEventHub)                               |
| `.Host.AzureServiceBus`              | Transport provider for Azure Service Bus                                                                            | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.AzureServiceBus.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.AzureServiceBus)                           |
| `.Host.Kafka`                        | Transport provider for Apache Kafka                                                                                 | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Kafka.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Kafka)                                               |
| `.Host.MQTT`                         | Transport provider for MQTT                                                                                         | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.MQTT.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.MQTT)                                                 |
| `.Host.Memory`                       | Transport provider implementation for in-process (in memory) message passing (no messaging infrastructure required) | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Memory.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Memory)                                             |
| `.Host.NATS`                         | Transport provider for [NATS](https://nats.io/)                                                                     | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.NATS.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.NATS)                                                 |
| `.Host.RabbitMQ`                     | Transport provider for RabbitMQ                                                                                     | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.RabbitMQ.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.RabbitMQ)                                         |
| `.Host.Redis`                        | Transport provider for Redis                                                                                        | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Redis.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Redis)                                               |
| `.Host.Sql` (pending)                | Transport provider implementation for SQL database message passing                                                  | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Sql.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Sql)                                                   |
| **Serialization**                    |                                                                                                                     |                                                                                                                                                                                  |
| `.Host.Serialization.Json`           | Serialization plugin for JSON (Newtonsoft.Json library)                                                             | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Serialization.Json.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.Json)                     |
| `.Host.Serialization.SystemTextJson` | Serialization plugin for JSON (System.Text.Json library)                                                            | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Serialization.SystemTextJson.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.SystemTextJson) |
| `.Host.Serialization.Avro`           | Serialization plugin for Avro (Apache.Avro library)                                                                 | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Serialization.Avro.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.Avro)                     |
| `.Host.Serialization.Hybrid`         | Plugin that delegates serialization to other serializers based on message type                                      | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Serialization.Hybrid.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.Hybrid)                 |
| `.Host.Serialization.GoogleProtobuf` | Serialization plugin for Google Protobuf                                                                            | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Serialization.GoogleProtobuf.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.GoogleProtobuf) |
| **Plugins**                          |                                                                                                                     |                                                                                                                                                                                  |
| `.Host.AspNetCore`                   | Integration for ASP.NET Core                                                                                        | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.AspNetCore.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.AspNetCore)                                     |
| `.Host.Interceptor`                  | Core interface for interceptors                                                                                     | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Interceptor.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Interceptor)                                   |
| `.Host.FluentValidation`             | Validation for messages based on [FluentValidation](https://www.nuget.org/packages/FluentValidation)                | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.FluentValidation.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.FluentValidation)                         |
| `.Host.Outbox.PostgreSql`            | Transactional Outbox using PostgreSQL                                                                               | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Outbox.PostgreSql.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.PostgreSql)                       |
| `.Host.Outbox.PostgreSql.DbContext`  | Transactional Outbox using PostgreSQL with EF DataContext integration                                               | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Outbox.PostgreSql.DbContext.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.PostgreSql.DbContext)   |
| `.Host.Outbox.Sql`                   | Transactional Outbox using MSSQL                                                                                    | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Outbox.Sql.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.Sql)                                     |
| `.Host.Outbox.Sql.DbContext`         | Transactional Outbox using MSSQL with EF DataContext integration                                                    | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.Outbox.Sql.DbContext.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.Outbox.Sql.DbContext)                 |
| `.Host.AsyncApi`                     | [AsyncAPI](https://www.asyncapi.com/) specification generation via [Saunter](https://github.com/tehmantra/saunter)  | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.AsyncApi.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.AsyncApi)                                         |
| `.Host.CircuitBreaker.HealthCheck`   | Consumer circuit breaker based on [health checks](docs/intro.md#health-check-circuit-breaker)                       | [![NuGet](https://img.shields.io/nuget/v/SlimMessageBus.Host.CircuitBreaker.HealthCheck.svg)](https://www.nuget.org/packages/SlimMessageBus.Host.CircuitBreaker.HealthCheck)     |

Typically the application layers (domain model, business logic) only need to depend on `SlimMessageBus` which is the facade, and ultimately the application hosting layer (ASP.NET, Console App, Windows Service) will reference and configure the other packages (`SlimMessageBus.Host.*`) which are the messaging transport providers and additional plugins.

## 🎯 Features

- Supports multiple messaging patterns: pub/sub, request-response, queues
- Compatible with popular brokers: Kafka, RabbitMQ, Azure EventHubs, MQTT, Redis, and more
- Fluent API for easy configuration
- Plugin architecture for serialization, validation, outbox patterns and [interceptor pipeline](docs/intro.md#interceptors)
- Integration with .NET dependency injection
- Modern async/await support
- Minimal external dependencies
- [SourceLink](docs/intro.md#debugging) support
- Because SlimMessageBus is a facade, chosen messaging transports can be swapped without impacting the overall application architecture.

## 🚧 Contributing

We welcome contributions! See [CONTRIBUTING.md](CONTRIBUTING.md) for details on submitting issues, feature requests, and pull requests.
See [here](docs/Maintainers/).

## 💬 Community

- Raise issues, discussions, questions and feature requests [here](https://github.com/zarusz/SlimMessageBus/issues).

## 📜 License

SlimMessageBus is licensed under the [Apache License 2.0](LICENSE).

## 🙌 Credits

Special thanks to:

- Our maintainers
- [Gravity9](https://www.gravity9.com/) for Azure infrastructure support.
- [Redis Labs](https://redislabs.com/), [CloudKarafka](https://www.cloudkarafka.com/), [HiveMQ](https://www.hivemq.com/), [CloudAMQP](https://www.cloudamqp.com/) for providing infrastructure for testing.
