using System;
using System.Linq;

namespace SlimMessageBus.Host.Config
{
    public class GroupSubscriberBuilder<T>
    {
        private readonly SubscriberSettings _subscriberSettings;
        public string Group { get; }

        public GroupSubscriberBuilder(string group, MessageBusSettings settings, TopicSubscriberBuilder<T> topicSubscriberBuilder)
        {
            Group = group;

            var subscriberSettingsExist = settings.Subscribers.Any(x => x.Group == group && x.Topic == topicSubscriberBuilder.Topic);
            Assert.IsFalse(subscriberSettingsExist, 
                () => new ConfigurationMessageBusException($"Group '{group}' configuration for topic '{topicSubscriberBuilder.Topic}' already exists"));

            _subscriberSettings = new SubscriberSettings()
            {
                MessageType = typeof(T),
                Topic = topicSubscriberBuilder.Topic,
                Group = group
            };
            settings.Subscribers.Add(_subscriberSettings);
        }

        public GroupSubscriberBuilder<T> WitSubscriber<TConsumer>()
            where TConsumer : ISubscriber<T>
        {
            _subscriberSettings.ConsumerType = typeof(TConsumer);
            _subscriberSettings.ConsumerMode = ConsumerMode.Subscriber;
            return this;
        }

        public GroupSubscriberBuilder<T> WithConsumer<TConsumer>()
        {
            return WithConsumer(typeof (TConsumer));
        }

        public GroupSubscriberBuilder<T> WithConsumer(Type consumerType)
        {
            _subscriberSettings.ConsumerType = consumerType;

            var genericInterfaces = consumerType.GetInterfaces().Where(x => x.IsGenericType).Select(x => x.GetGenericTypeDefinition()).ToList();
            if (genericInterfaces.Contains(typeof (ISubscriber<>)))
            {
                _subscriberSettings.ConsumerMode = ConsumerMode.Subscriber;
            }
            else if (genericInterfaces.Contains(typeof (IRequestHandler<,>)))
            {
                _subscriberSettings.ConsumerMode = ConsumerMode.RequestResponse;
            }
            else
            {
                throw new ConfigurationMessageBusException($"The consumer type {consumerType} has to implement one of these interfaces: {typeof(ISubscriber<>)} or {typeof(IRequestHandler<,>)}");
            }
            return this;
        }

        public GroupSubscriberBuilder<T> Instances(int numberOfInstances)
        {
            _subscriberSettings.Instances = numberOfInstances;
            return this;
        }
    }
}