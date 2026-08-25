namespace SlimMessageBus.Host.GooglePubSub;

internal static class GooglePubSubResourceName
{
    internal static TopicName ResolveTopic(string projectId, string path)
        => path.StartsWith("projects/", StringComparison.Ordinal)
            ? TopicName.Parse(path)
            : TopicName.FromProjectTopic(projectId, path);

    internal static SubscriptionName ResolveSubscription(string projectId, string path)
        => path.StartsWith("projects/", StringComparison.Ordinal)
            ? SubscriptionName.Parse(path)
            : SubscriptionName.FromProjectSubscription(projectId, path);
}
