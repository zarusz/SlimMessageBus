namespace SlimMessageBus.Host.AzureServiceBus;

public class TopicSubscriptionParams(string path, string subscriptionName)
{
    public string Path { get; set; } = path;
    public string SubscriptionName { get; set; } = subscriptionName;

    public override string ToString()
        => SubscriptionName == null ? Path : $"{Path}/{SubscriptionName}";
}