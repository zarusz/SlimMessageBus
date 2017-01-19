using System;

namespace SlimMessageBus.Host.Config
{
    public class GroupSubscriberBuilder<TMessage> : GroupConsumerBuilder<TMessage>
    {
        public GroupSubscriberBuilder(string group, string topic, MessageBusSettings settings)
            : base(group, topic, settings)
        {
        }

        public GroupSubscriberBuilder<TMessage> WithSubscriber<TConsumer>()
            where TConsumer : ISubscriber<TMessage>
        {
            ConsumerSettings.ConsumerType = typeof(TConsumer);
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