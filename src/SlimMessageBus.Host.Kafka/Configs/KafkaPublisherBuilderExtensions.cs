using System;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.Kafka
{
    public static class KafkaPublisherBuilderExtensions
    {
        /// <summary>
        /// Sets the key provider for the message type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="mbb"></param>
        /// <param name="keyProvider">Delegate to determine the key for an message. Parameter meaning: (message, topic) => key.</param>
        /// <remarks>Ensure the implementation is thread-safe.</remarks>
        /// <returns></returns>
        public static ProducerBuilder<T> KeyProvider<T>(this ProducerBuilder<T> mbb, Func<T, string, byte[]> keyProvider)
        {
            Assert.IsNotNull(keyProvider, () => new ConfigurationMessageBusException("Null value provided"));

            byte[] UntypedProvider(object message, string topic) => keyProvider((T) message, topic);
            mbb.Settings.Properties[KafkaPublisherSettingsExtensions.KeyProviderKey] = (Func<object, string, byte[]>) UntypedProvider;
            return mbb;
        }

        /// <summary>
        /// Sets the partition provider for the message type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="mbb"></param>
        /// <param name="partitionProvider">Delegate to determine the partition number for an message. Parameter meaning: (message, topic) => partition.</param>
        /// <remarks>Ensure the implementation is thread-safe.</remarks>
        /// <returns></returns>
        public static ProducerBuilder<T> PartitionProvider<T>(this ProducerBuilder<T> mbb, Func<T, string, int> partitionProvider)
        {
            Assert.IsNotNull(partitionProvider, () => new ConfigurationMessageBusException("Null value provided"));

            int UntypedProvider(object message, string topic) => partitionProvider((T) message, topic);
            mbb.Settings.Properties[KafkaPublisherSettingsExtensions.PartitionProviderKey] = (Func<object, string, int>) UntypedProvider;
            return mbb;
        }

    }
}