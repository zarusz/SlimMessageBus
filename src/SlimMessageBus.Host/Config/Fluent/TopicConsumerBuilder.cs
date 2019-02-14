using System;
using System.Linq;
using System.Reflection;

namespace SlimMessageBus.Host.Config
{
    public class TopicConsumerBuilder<TMessage> : AbstractTopicConsumerBuilder
    {
        public TopicConsumerBuilder(string topic, Type messageType, MessageBusSettings settings)
            : base(topic, messageType, settings)
        {
        }

        public TopicConsumerBuilder<TMessage> WithConsumer<TConsumer>()
            where TConsumer : IConsumer<TMessage>
        {
            return WithConsumer(typeof(TConsumer));
        }

        public TopicConsumerBuilder<TMessage> WithConsumer(Type consumerType)
        {
            Assert.IsTrue(
                consumerType.GetInterfaces().Any(x => x.IsConstructedGenericType && x.GetGenericTypeDefinition() == typeof(IConsumer<>) && x.GetGenericArguments()[0] == ConsumerSettings.MessageType),
                () => new InvalidConfigurationMessageBusException($"The consumer type {consumerType} needs to implement interface {typeof(IConsumer<>).MakeGenericType(ConsumerSettings.MessageType)}"));

            ConsumerSettings.ConsumerType = consumerType;
            ConsumerSettings.ConsumerMode = ConsumerMode.Consumer;
            return this;
        }

        public TopicConsumerBuilder<TMessage> Instances(int numberOfInstances)
        {
            ConsumerSettings.Instances = numberOfInstances;
            return this;
        }
    }
}