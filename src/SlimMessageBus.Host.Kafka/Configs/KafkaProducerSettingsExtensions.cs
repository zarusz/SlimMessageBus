namespace SlimMessageBus.Host.Kafka;

public static class KafkaProducerSettingsExtensions
{
    internal const string KeyProviderKey = "Kafka_KeyProvider";
    internal const string PartitionProviderKey = "Kafka_PartitionProvider";
    internal const string EnableProduceAwaitKey = "Kafka_AwaitProduce";

    public static KafkaKeyProvider<object> GetKeyProvider(this ProducerSettings ps)
        => ps.GetOrDefault<KafkaKeyProvider<object>>(KeyProviderKey, null);

    public static KafkaPartitionProvider<object> GetPartitionProvider(this ProducerSettings ps)
        => ps.GetOrDefault<KafkaPartitionProvider<object>>(PartitionProviderKey, null);
}
