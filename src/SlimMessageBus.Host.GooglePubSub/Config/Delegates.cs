namespace SlimMessageBus.Host.GooglePubSub;

/// <summary>
/// Allows the native Google Cloud Pub/Sub message to be customized before it is published.
/// </summary>
public delegate void GooglePubSubMessageModifier<in T>(T message, PubsubMessage transportMessage);

/// <summary>
/// Intercepts topology provisioning and can run custom logic before or after it.
/// </summary>
public delegate Task GooglePubSubTopologyInterceptor(
    PublisherServiceApiClient publisherClient,
    SubscriberServiceApiClient subscriberClient,
    Func<Task> provision,
    CancellationToken cancellationToken);
