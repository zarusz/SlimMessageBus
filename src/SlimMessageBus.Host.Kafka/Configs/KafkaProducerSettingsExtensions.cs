namespace SlimMessageBus.Host.Kafka;

public static class KafkaProducerSettingsExtensions
{
    internal const string KeyProviderKey = "Kafka_KeyProvider";
    internal const string PartitionProviderKey = "Kafka_PartitionProvider";

    public static Func<object, string, byte[]> GetKeyProvider(this ProducerSettings ps)
    {
        return ps.GetOrDefault<Func<object, string, byte[]>>(KeyProviderKey, null);
    }

    public static Func<object, string, int> GetPartitionProvider(this ProducerSettings ps)
    {
        return ps.GetOrDefault<Func<object, string, int>>(PartitionProviderKey, null);
    }
}