namespace SlimMessageBus.Host.GooglePubSub;

internal class GooglePubSubTopologyService(
    ILogger<GooglePubSubTopologyService> logger,
    MessageBusSettings settings,
    GooglePubSubMessageBusSettings providerSettings,
    PublisherServiceApiClient publisherClient,
    SubscriberServiceApiClient subscriberClient)
{
    private static bool IsNotFound(RpcException exception) => exception.StatusCode == StatusCode.NotFound;
    private static bool IsAlreadyExists(RpcException exception) => exception.StatusCode == StatusCode.AlreadyExists;

    internal async Task EnsureTopic(
        string path,
        bool canCreate,
        IEnumerable<Action<Topic>> configureActions,
        CancellationToken cancellationToken)
    {
        var topicName = GooglePubSubResourceName.ResolveTopic(providerSettings.ProjectId, path);
        try
        {
            await publisherClient.GetTopicAsync(topicName, cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }
        catch (RpcException exception) when (IsNotFound(exception))
        {
            // The topic will be created below when topology provisioning permits it.
        }

        if (!canCreate)
        {
            logger.LogWarning("Topic {Topic} does not exist and topic creation is disabled", topicName);
            return;
        }

        var topic = new Topic { TopicName = topicName };
        providerSettings.TopologyProvisioning.CreateTopicOptions?.Invoke(topic);
        foreach (var configure in configureActions.Where(x => x != null))
        {
            configure(topic);
        }
        topic.TopicName = topicName;

        try
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Creating Google Pub/Sub topic {Topic}", topicName);
            }
            await publisherClient.CreateTopicAsync(topic, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (RpcException exception) when (IsAlreadyExists(exception))
        {
            // Another application instance created the topic concurrently.
        }
    }

    internal async Task EnsureSubscription(
        string topicPath,
        string subscriptionPath,
        IEnumerable<Action<Subscription>> configureActions,
        CancellationToken cancellationToken)
    {
        var topicName = GooglePubSubResourceName.ResolveTopic(providerSettings.ProjectId, topicPath);
        var subscriptionName = GooglePubSubResourceName.ResolveSubscription(providerSettings.ProjectId, subscriptionPath);

        try
        {
            var existing = await subscriberClient.GetSubscriptionAsync(subscriptionName, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!existing.TopicAsTopicName.Equals(topicName))
            {
                throw new ConfigurationMessageBusException($"Google Pub/Sub subscription {subscriptionName} already belongs to topic {existing.Topic}; it cannot also consume from {topicName}.");
            }
            return;
        }
        catch (RpcException exception) when (IsNotFound(exception))
        {
            // The subscription will be created below when topology provisioning permits it.
        }

        if (!providerSettings.TopologyProvisioning.CanConsumerCreateSubscription)
        {
            logger.LogWarning("Subscription {Subscription} does not exist and subscription creation is disabled", subscriptionName);
            return;
        }

        var subscription = new Subscription
        {
            SubscriptionName = subscriptionName,
            TopicAsTopicName = topicName
        };
        providerSettings.TopologyProvisioning.CreateSubscriptionOptions?.Invoke(subscription);
        foreach (var configure in configureActions.Where(x => x != null))
        {
            configure(subscription);
        }

        if (subscription.PushConfig != null || subscription.BigqueryConfig != null || subscription.CloudStorageConfig != null)
        {
            throw new ConfigurationMessageBusException($"Google Pub/Sub subscription {subscriptionName} must be a pull subscription.");
        }

        // Resource identity always comes from the SlimMessageBus declaration.
        subscription.SubscriptionName = subscriptionName;
        subscription.TopicAsTopicName = topicName;

        try
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Creating Google Pub/Sub subscription {Subscription} on topic {Topic}", subscriptionName, topicName);
            }
            await subscriberClient.CreateSubscriptionAsync(subscription, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (RpcException exception) when (IsAlreadyExists(exception))
        {
            // Another application instance created the subscription concurrently.
        }
    }

    internal Task Provision(CancellationToken cancellationToken)
        => providerSettings.TopologyProvisioning.OnProvisionTopology(
            publisherClient,
            subscriberClient,
            () => ProvisionCore(cancellationToken),
            cancellationToken);

    private async Task ProvisionCore(CancellationToken cancellationToken)
    {
        logger.LogInformation("Google Pub/Sub topology provisioning started");

        var consumerGroups = settings.Consumers.Cast<AbstractConsumerSettings>()
            .Concat(settings.RequestResponse == null ? [] : [settings.RequestResponse])
            .GroupBy(x => (Topic: x.Path, Subscription: x.GetSubscriptionName()));

        foreach (var group in consumerGroups)
        {
            var declarations = group.ToList();
            await EnsureTopic(
                group.Key.Topic,
                providerSettings.TopologyProvisioning.CanConsumerCreateTopic,
                declarations.Select(x => x.GetOrDefault(GooglePubSubProperties.CreateTopicOptions)),
                cancellationToken).ConfigureAwait(false);

            await EnsureSubscription(
                group.Key.Topic,
                group.Key.Subscription,
                declarations.Select(x => x.GetOrDefault(GooglePubSubProperties.CreateSubscriptionOptions)),
                cancellationToken).ConfigureAwait(false);
        }

        foreach (var producer in settings.Producers.Where(x => !string.IsNullOrWhiteSpace(x.DefaultPath)))
        {
            await EnsureTopic(
                producer.DefaultPath,
                providerSettings.TopologyProvisioning.CanProducerCreateTopic,
                [producer.GetOrDefault(GooglePubSubProperties.CreateTopicOptions)],
                cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation("Google Pub/Sub topology provisioning finished");
    }
}
