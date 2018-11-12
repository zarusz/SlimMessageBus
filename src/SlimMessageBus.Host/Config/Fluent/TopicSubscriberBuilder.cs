using System;
using System.Linq;
using System.Reflection;

namespace SlimMessageBus.Host.Config
{
    public class TopicSubscriberBuilder<TMessage> : TopicConsumerBuilder<TMessage>
    {
        public TopicSubscriberBuilder(string topic, Type messageType, MessageBusSettings settings)
            : base(topic, messageType, settings)
        {
        }

        public TopicSubscriberBuilder<TMessage> WithSubscriber<TConsumer>()
            where TConsumer : IConsumer<TMessage>
        {
            return WithSubscriber(typeof(TConsumer));
        }

        public TopicSubscriberBuilder<TMessage> WithSubscriber(Type consumerType)
        {
            Assert.IsTrue(
                consumerType.GetInterfaces().Any(x => x.IsConstructedGenericType && x.GetGenericTypeDefinition() == typeof(IConsumer<>) && x.GetGenericArguments()[0] == ConsumerSettings.MessageType),
                () => new InvalidConfigurationMessageBusException($"The subscriber type {consumerType} needs to implement interface {typeof(IConsumer<>).MakeGenericType(ConsumerSettings.MessageType)}"));

            ConsumerSettings.ConsumerType = consumerType;
            ConsumerSettings.ConsumerMode = ConsumerMode.Subscriber;
            return this;
        }

        public TopicSubscriberBuilder<TMessage> Instances(int numberOfInstances)
        {
            ConsumerSettings.Instances = numberOfInstances;
            return this;
        }
    }
}