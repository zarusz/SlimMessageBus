using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureServiceBus.Config
{
    public static class ProducerBuilderExtensions
    {
        public static ProducerBuilder<T> DefaultQueue<T>(this ProducerBuilder<T> producerBuilder, string queue)
        {
            producerBuilder.DefaultTopic(queue);
            producerBuilder.ToQueue();
            return producerBuilder;
        }

        /// <summary>
        /// The topic parameter name in <see cref="IPublishBus.Publish{TMessage}"/> should be treated as a Azure ServiceBus topic name
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="producerBuilder"></param>
        /// <returns></returns>
        public static ProducerBuilder<T> ToTopic<T>(this ProducerBuilder<T> producerBuilder)
        {
            producerBuilder.Settings.SetKind(PathKind.Topic);
            return producerBuilder;
        }

        /// <summary>
        /// The topic parameter name in <see cref="IPublishBus.Publish{TMessage}"/> should be treated as a Azure ServiceBus queue name
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="producerBuilder"></param>
        /// <returns></returns>
        public static ProducerBuilder<T> ToQueue<T>(this ProducerBuilder<T> producerBuilder)
        {
            producerBuilder.Settings.SetKind(PathKind.Queue);
            return producerBuilder;
        }
    }
}
