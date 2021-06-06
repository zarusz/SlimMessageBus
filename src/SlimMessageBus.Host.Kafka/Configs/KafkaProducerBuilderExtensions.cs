namespace SlimMessageBus.Host.Kafka
{
    using System;
    using SlimMessageBus.Host.Config;

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
        public static ProducerBuilder<T> KeyProvider<T>(this ProducerBuilder<T> builder, Func<T, string, byte[]> keyProvider)
        {
            Assert.IsNotNull(keyProvider, () => new ConfigurationMessageBusException("Null value provided"));

            byte[] UntypedProvider(object message, string topic) => keyProvider((T) message, topic);
            builder.Settings.Properties[KafkaProducerSettingsExtensions.KeyProviderKey] = (Func<object, string, byte[]>) UntypedProvider;
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
        public static ProducerBuilder<T> PartitionProvider<T>(this ProducerBuilder<T> builder, Func<T, string, int> partitionProvider)
        {
            Assert.IsNotNull(partitionProvider, () => new ConfigurationMessageBusException("Null value provided"));

            int UntypedProvider(object message, string topic) => partitionProvider((T) message, topic);
            builder.Settings.Properties[KafkaProducerSettingsExtensions.PartitionProviderKey] = (Func<object, string, int>) UntypedProvider;
            return builder;
        }

    }
}