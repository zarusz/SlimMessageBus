using System;
using Microsoft.Azure.ServiceBus;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureServiceBus
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

        /// <summary>
        /// Allows to set additional properties to the native <see cref="Message"/> when producing the <see cref="T"/> message.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="producerBuilder"></param>
        /// <param name="modifierAction"></param>
        /// <returns></returns>
        public static ProducerBuilder<T> WithModifier<T>(this ProducerBuilder<T> producerBuilder, Action<T, Message> modifierAction)
        {
            producerBuilder.Settings.SetMessageModifier((e, m) =>
            {
               modifierAction((T) e, m);
            });
            return producerBuilder;
        }
    }
}
