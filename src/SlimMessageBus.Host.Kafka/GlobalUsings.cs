global using Confluent.Kafka;
global using Confluent.Kafka.Admin;

global using Microsoft.Extensions.Logging;

global using SlimMessageBus.Host.Collections;
global using SlimMessageBus.Host.Serialization;
global using SlimMessageBus.Host.Services;

global using ConsumerBuilder = Confluent.Kafka.ConsumerBuilder<Confluent.Kafka.Ignore, byte[]>;
global using IProducer = Confluent.Kafka.IProducer<byte[], byte[]>;
global using Message = Confluent.Kafka.Message<byte[], byte[]>;
global using ProducerBuilder = Confluent.Kafka.ProducerBuilder<byte[], byte[]>;

