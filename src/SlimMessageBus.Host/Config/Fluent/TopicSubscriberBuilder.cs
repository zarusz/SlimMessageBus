using System;

namespace SlimMessageBus.Host.Config
{
    public class TopicSubscriberBuilder<T> : TopicConsumerBuilder<T>
    {
        public TopicSubscriberBuilder(string topic, Type messageType, MessageBusSettings settings)
            : base(topic, messageType, settings)
        {
        }

        public GroupSubscriberBuilder<T> Group(string group)
        {
            return new GroupSubscriberBuilder<T>(group, Topic, MessageType, Settings);
        }
    }
}