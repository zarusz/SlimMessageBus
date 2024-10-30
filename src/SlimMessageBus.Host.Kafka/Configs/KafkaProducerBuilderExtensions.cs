namespace SlimMessageBus.Host.Kafka;

public static class KafkaProducerBuilderExtensions
{
    /// <summary>
    /// Sets the key provider for the message type
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder"></param>
    /// <param name="keyProvider">Delegate to determine the key for an message. Parameter meaning: (message, topic) => key.</param>
    /// <remarks>Ensure the implementation is thread-safe.</remarks>
    /// <returns></returns>
    public static ProducerBuilder<T> KeyProvider<T>(this ProducerBuilder<T> builder, KafkaKeyProvider<T> keyProvider)
    {
        Assert.IsNotNull(keyProvider, () => new ConfigurationMessageBusException("Null value provided"));

        byte[] UntypedProvider(object message, string topic) => keyProvider((T)message, topic);
        builder.Settings.Properties[KafkaProducerSettingsExtensions.KeyProviderKey] = (KafkaKeyProvider<object>)UntypedProvider;
        return builder;
    }

    /// <summary>
    /// Sets the partition provider for the message type
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder"></param>
    /// <param name="partitionProvider">Delegate to determine the partition number for an message. Parameter meaning: (message, topic) => partition.</param>
    /// <remarks>Ensure the implementation is thread-safe.</remarks>
    /// <returns></returns>
    public static ProducerBuilder<T> PartitionProvider<T>(this ProducerBuilder<T> builder, KafkaPartitionProvider<T> partitionProvider)
    {
        Assert.IsNotNull(partitionProvider, () => new ConfigurationMessageBusException("Null value provided"));

        int UntypedProvider(object message, string topic) => partitionProvider((T)message, topic);
        builder.Settings.Properties[KafkaProducerSettingsExtensions.PartitionProviderKey] = (KafkaPartitionProvider<object>)UntypedProvider;
        return builder;
    }

    /// <summary>
    /// Enables (or disables) awaiting for the message delivery result during producing of the message to the Kafka topic.
    /// Allows to increase the throughput of the producer, but may lead to message loss in case of message delivery failures.
    /// Internally the kafka driver will buffer the messages and deliver them in batches.
    /// By default this is enabled.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder"></param>
    /// <param name="enable"></param>
    /// <returns></returns>
    public static TBuilder EnableProduceAwait<TBuilder>(this TBuilder builder, bool enable = true)
        where TBuilder : IProducerBuilder
    {
        builder.Settings.Properties[KafkaProducerSettingsExtensions.EnableProduceAwaitKey] = enable;
        return builder;
    }
}