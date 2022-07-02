namespace SlimMessageBus.Host.AzureServiceBus;

public class TopicSubscriptionParams
{
    public string Path { get; set; }
    public string SubscriptionName { get; set; }

    public TopicSubscriptionParams(string path, string subscriptionName)
    {
        Path = path;
        SubscriptionName = subscriptionName;
    }

    public override string ToString()
        => SubscriptionName == null ? Path : $"{Path}/{SubscriptionName}";
}