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
        public static PublisherBuilder<T> KeyProvider<T>(this PublisherBuilder<T> mbb, Func<T, string, byte[]> keyProvider)
        {
            Assert.IsNotNull(keyProvider, () => new ConfigurationMessageBusException("Null value provided"));

            Func<object, string, byte[]> untypedProvider = (message, topic) => keyProvider((T)message, topic);
            mbb.Settings.Properties[KafkaPublisherSettingsExtensions.KeyProviderKey] = untypedProvider;
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
        public static PublisherBuilder<T> PartitionProvider<T>(this PublisherBuilder<T> mbb, Func<T, string, int> partitionProvider)
        {
            Assert.IsNotNull(partitionProvider, () => new ConfigurationMessageBusException("Null value provided"));

            Func<object, string, int> untypedProvider = (message, topic) => partitionProvider((T)message, topic);
            mbb.Settings.Properties[KafkaPublisherSettingsExtensions.PartitionProviderKey] = untypedProvider;
            return mbb;
        }

    }
}