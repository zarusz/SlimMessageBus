namespace SlimMessageBus.Host.Kafka;

public delegate byte[] KafkaKeyProvider<in T>(T message, string topic);

public delegate int KafkaPartitionProvider<in T>(T message, string topic);
