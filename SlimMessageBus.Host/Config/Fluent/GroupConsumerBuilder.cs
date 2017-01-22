using System.Linq;

namespace SlimMessageBus.Host.Config
{
    public abstract class GroupConsumerBuilder<TMessage>
    {
        protected readonly ConsumerSettings ConsumerSettings;
        public string Group { get; }

        protected GroupConsumerBuilder(string group, string topic, MessageBusSettings settings)
        {
            Group = group;

            var consumerSettingsExist = settings.Consumers.Any(x => x.Group == group && x.Topic == topic);
            Assert.IsFalse(consumerSettingsExist,
                () => new ConfigurationMessageBusException($"Group '{group}' configuration for topic '{topic}' already exists"));

            ConsumerSettings = new ConsumerSettings
            {
                Group = group,
                Topic = topic,
                MessageType = typeof(TMessage)
            };
            settings.Consumers.Add(ConsumerSettings);
        }

    }
}