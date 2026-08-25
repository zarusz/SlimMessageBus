namespace SlimMessageBus.Host.GooglePubSub;

public class GooglePubSubMessageBusSettings : HasProviderExtensions
{
    /// <summary>
    /// Google Cloud project used when topic and subscription IDs are not fully qualified resource names.
    /// </summary>
    public string ProjectId { get; set; }

    /// <summary>
    /// Creates a long-lived publisher for a topic. Defaults to Application Default Credentials.
    /// </summary>
    public Func<IServiceProvider, TopicName, CancellationToken, Task<PublisherClient>> PublisherClientFactory { get; set; }
        = (_, topic, _) => PublisherClient.CreateAsync(topic);

    /// <summary>
    /// Creates a long-lived pull subscriber. Defaults to Application Default Credentials.
    /// </summary>
    public Func<IServiceProvider, SubscriptionName, CancellationToken, Task<SubscriberClient>> SubscriberClientFactory { get; set; }
        = (_, subscription, _) => SubscriberClient.CreateAsync(subscription);

    /// <summary>
    /// Creates the topic administration client used by topology provisioning.
    /// </summary>
    public Func<IServiceProvider, CancellationToken, Task<PublisherServiceApiClient>> PublisherServiceApiClientFactory { get; set; }
        = (_, _) => PublisherServiceApiClient.CreateAsync();

    /// <summary>
    /// Creates the subscription administration client used by topology provisioning.
    /// </summary>
    public Func<IServiceProvider, CancellationToken, Task<SubscriberServiceApiClient>> SubscriberServiceApiClientFactory { get; set; }
        = (_, _) => SubscriberServiceApiClient.CreateAsync();

    public IGooglePubSubHeaderSerializer HeaderSerializer { get; set; } = new DefaultGooglePubSubHeaderSerializer();

    /// <summary>
    /// Topology provisioning settings. Provisioning is enabled by default.
    /// </summary>
    public GooglePubSubTopologySettings TopologyProvisioning { get; set; } = new();

    public GooglePubSubMessageBusSettings WithModifier(GooglePubSubMessageModifier<object> modifier, bool executePrevious = true)
    {
        if (modifier is null) throw new ArgumentNullException(nameof(modifier));

        var previous = executePrevious ? GetOrDefault(GooglePubSubProperties.MessageModifier) : null;
        GooglePubSubProperties.MessageModifier.Set(this, previous == null
            ? modifier
            : (message, transportMessage) =>
            {
                previous(message, transportMessage);
                modifier(message, transportMessage);
            });

        return this;
    }

    public GooglePubSubMessageBusSettings WithModifier<T>(GooglePubSubMessageModifier<T> modifier, bool executePrevious = true)
    {
        if (modifier is null) throw new ArgumentNullException(nameof(modifier));

        return WithModifier((message, transportMessage) =>
        {
            if (message is T typedMessage)
            {
                modifier(typedMessage, transportMessage);
            }
        }, executePrevious);
    }

    /// <summary>
    /// Uses a trusted service-account credential JSON file instead of Application Default Credentials.
    /// </summary>
    public GooglePubSubMessageBusSettings UseCredentialsPath(string credentialsPath)
    {
        if (credentialsPath is null) throw new ArgumentNullException(nameof(credentialsPath));
        var credential = CredentialFactory.FromFile<ServiceAccountCredential>(credentialsPath).ToGoogleCredential();
        return UseGoogleCredential(credential);
    }

    /// <summary>
    /// Uses an explicitly created Google credential instead of Application Default Credentials.
    /// </summary>
    public GooglePubSubMessageBusSettings UseGoogleCredential(GoogleCredential credential)
    {
        if (credential is null) throw new ArgumentNullException(nameof(credential));

        PublisherClientFactory = (_, topic, cancellationToken) => new PublisherClientBuilder
        {
            TopicName = topic,
            GoogleCredential = credential
        }.BuildAsync(cancellationToken);
        SubscriberClientFactory = (_, subscription, cancellationToken) => new SubscriberClientBuilder
        {
            SubscriptionName = subscription,
            GoogleCredential = credential
        }.BuildAsync(cancellationToken);
        PublisherServiceApiClientFactory = (_, cancellationToken) => new PublisherServiceApiClientBuilder
        {
            GoogleCredential = credential
        }.BuildAsync(cancellationToken);
        SubscriberServiceApiClientFactory = (_, cancellationToken) => new SubscriberServiceApiClientBuilder
        {
            GoogleCredential = credential
        }.BuildAsync(cancellationToken);

        return this;
    }
}
