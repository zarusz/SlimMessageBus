using System;
using System.Linq;
using System.Reflection;

namespace SlimMessageBus.Host.Config
{
    public class GroupSubscriberBuilder<TMessage> : GroupConsumerBuilder<TMessage>
    {
        public GroupSubscriberBuilder(string group, string topic, Type messageType, MessageBusSettings settings)
            : base(group, topic, messageType, settings)
        {
        }

        public GroupSubscriberBuilder<TMessage> WithSubscriber<TConsumer>()
            where TConsumer : IConsumer<TMessage>
        {
            ConsumerSettings.ConsumerType = typeof(TConsumer);
            ConsumerSettings.ConsumerMode = ConsumerMode.Subscriber;
            return this;
        }

        public GroupSubscriberBuilder<TMessage> WithSubscriber(Type consumerType)
        {
            Assert.IsTrue(
                consumerType.GetInterfaces().Any(x => x.IsConstructedGenericType && x.GetGenericTypeDefinition() == typeof(IConsumer<>) && x.GetGenericArguments()[0] == ConsumerSettings.MessageType),
                () => new InvalidConfigurationMessageBusException($"The subscriber type {consumerType} needs to implement interface {typeof(IConsumer<>).MakeGenericType(ConsumerSettings.MessageType)}"));

            ConsumerSettings.ConsumerType = consumerType;
            ConsumerSettings.ConsumerMode = ConsumerMode.Subscriber;
            return this;
        }

        public GroupSubscriberBuilder<TMessage> Instances(int numberOfInstances)
        {
            ConsumerSettings.Instances = numberOfInstances;
            return this;
        }
    }
}