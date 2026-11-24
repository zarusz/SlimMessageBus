# SlimMessageBus provider lookup

The package suffixes below follow `SlimMessageBus.Host.`. Check the documentation and samples for the application's installed release before using a provider API.

| Provider documentation | Package suffix | Builder method | Routing to check |
| --- | --- | --- | --- |
| [Memory](https://github.com/zarusz/SlimMessageBus/blob/master/docs/provider_memory.md) | `Memory` | `WithProviderMemory` | Match virtual paths within the process. Serialization is opt-in. |
| [Apache Kafka](https://github.com/zarusz/SlimMessageBus/blob/master/docs/provider_kafka.md) | `Kafka` | `WithProviderKafka` | Set `BrokerList` and `KafkaGroup(...)` on consumers. Check the reply consumer group for requests. |
| [RabbitMQ](https://github.com/zarusz/SlimMessageBus/blob/master/docs/provider_rabbitmq.md) | `RabbitMQ` | `WithProviderRabbitMQ` | Publish to an exchange. Consume from a queue bound to the exchange with the appropriate routing key. |
| [Azure Service Bus](https://github.com/zarusz/SlimMessageBus/blob/master/docs/provider_azure_servicebus.md) | `AzureServiceBus` | `WithProviderServiceBus` | Use queue declarations for queues. Topic consumers use a topic and `SubscriptionName(...)`. |
| [Azure Event Hubs](https://github.com/zarusz/SlimMessageBus/blob/master/docs/provider_azure_eventhubs.md) | `AzureEventHub` | `WithProviderEventHub` | Check the hub, `EventHubGroup(...)`, and blob storage for checkpointing and ownership. |
| [Amazon SQS/SNS](https://github.com/zarusz/SlimMessageBus/blob/master/docs/provider_amazon_sqs.md) | `AmazonSQS` | `WithProviderAmazonSQS` | Distinguish direct SQS publishing from SNS fan-out to subscribed SQS queues. |
| [Google Cloud Pub/Sub](https://github.com/zarusz/SlimMessageBus/blob/master/docs/provider_google_pubsub.md) | `GooglePubSub` | `WithProviderGooglePubSub` | Match the project, topic, and pull subscription. |
| [MQTT](https://github.com/zarusz/SlimMessageBus/blob/master/docs/provider_mqtt.md) | `MQTT` | `WithProviderMqtt` | Check publish topics, subscription filters, and QoS. |
| [NATS](https://github.com/zarusz/SlimMessageBus/blob/master/docs/provider_nats.md) | `NATS` | `WithProviderNats` | Use Core NATS subjects and check queue-group and native request/reply behavior. The builder extension is in `SlimMessageBus.Host.Nats.Config`. |
| [Redis](https://github.com/zarusz/SlimMessageBus/blob/master/docs/provider_redis.md) | `Redis` | `WithProviderRedis` | Match the destination and choose between Redis Pub/Sub channels and list-backed queues. |
| [SQL Server / Azure SQL](https://github.com/zarusz/SlimMessageBus/blob/master/docs/provider_sql.md) | `Sql` | `WithProviderSql` | Check logical queues or topics, subscription names, and database migrations. |
| [PostgreSQL](https://github.com/zarusz/SlimMessageBus/blob/master/docs/provider_postgresql.md) | `PostgreSql` | `WithProviderPostgreSql` | Check logical queues or topics, subscription names, and database migrations. |

See the [samples index](https://github.com/zarusz/SlimMessageBus/blob/master/src/Samples/README.md) for complete host configurations.
