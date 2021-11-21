namespace SlimMessageBus.Host.AzureEventHub
{
    public class TopicGroup
    {
        public TopicGroup(string topic, string group)
        {
            Topic = topic;
            Group = group;
        }

        public string Topic { get; }
        public string Group { get; }
    }
}