namespace SlimMessageBus.Host.GooglePubSub;

public class GooglePubSubTopologySettings
{
    /// <summary>
    /// Indicates whether declared topics and subscriptions are created when missing. Default is true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    public bool CanProducerCreateTopic { get; set; } = true;
    public bool CanConsumerCreateTopic { get; set; } = true;
    public bool CanConsumerCreateSubscription { get; set; } = true;

    /// <summary>
    /// Default customization applied when a topic is created.
    /// </summary>
    public Action<Topic> CreateTopicOptions { get; set; }

    /// <summary>
    /// Default customization applied when a pull subscription is created.
    /// </summary>
    public Action<Subscription> CreateSubscriptionOptions { get; set; }

    public GooglePubSubTopologyInterceptor OnProvisionTopology { get; set; }
        = (_, _, provision, _) => provision();
}
