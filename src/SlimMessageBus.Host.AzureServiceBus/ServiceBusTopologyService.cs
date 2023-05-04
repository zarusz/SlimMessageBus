namespace SlimMessageBus.Host.AzureServiceBus;

using Azure;

public class ServiceBusTopologyService
{
    private readonly ILogger<ServiceBusTopologyService> logger;
    private readonly MessageBusSettings settings;
    private readonly ServiceBusMessageBusSettings providerSettings;
    private readonly ServiceBusAdministrationClient adminClient;

    public ServiceBusTopologyService(ILogger<ServiceBusTopologyService> logger, MessageBusSettings settings, ServiceBusMessageBusSettings providerSettings)
    {
        this.logger = logger;
        this.settings = settings;
        this.providerSettings = providerSettings;
        this.adminClient = providerSettings.AdminClientFactory();
    }

    [Flags]
    private enum TopologyCreationStatus
    {
        NotExists = 0,
        Exists = 1,
        Created = 2
    }

    private async Task<TopologyCreationStatus> SwallowExceptionIfEntityExists(Func<Task<TopologyCreationStatus>> task)
    {
        try
        {
            return await task();
        }
        catch (ServiceBusException e) when (e.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
            // do nothing as another service instance might have created that in the meantime
            return TopologyCreationStatus.Exists;
        }
    }

    private Task<TopologyCreationStatus> TryCreateQueue(string path, bool canCreate, Action<CreateQueueOptions> action) => SwallowExceptionIfEntityExists(async () =>
    {
        if (await adminClient.QueueExistsAsync(path)) return TopologyCreationStatus.Exists;

        if (!canCreate)
        {
            logger.LogWarning("Queue {Path} does not exist and queue creation was not allowed", path);
            return TopologyCreationStatus.NotExists;
        }

        var options = new CreateQueueOptions(path);
        providerSettings.TopologyProvisioning?.CreateQueueOptions?.Invoke(options);
        action?.Invoke(options);

        logger.LogInformation("Creating queue: {Path} ...", path);
        await adminClient.CreateQueueAsync(options);

        return TopologyCreationStatus.Exists | TopologyCreationStatus.Created;
    });

    private Task<TopologyCreationStatus> TryCreateTopic(string path, bool canCreate, Action<CreateTopicOptions> action) => SwallowExceptionIfEntityExists(async () =>
    {
        if (await adminClient.TopicExistsAsync(path)) return TopologyCreationStatus.Exists;

        if (!canCreate)
        {
            logger.LogWarning("Topic {Path} does not exist and topic creation was not allowed", path);
            return TopologyCreationStatus.NotExists;
        }

        var options = new CreateTopicOptions(path);
        providerSettings.TopologyProvisioning?.CreateTopicOptions?.Invoke(options);
        action?.Invoke(options);

        logger.LogInformation("Creating topic: {Path} ...", path);
        await adminClient.CreateTopicAsync(options);

        return TopologyCreationStatus.Exists | TopologyCreationStatus.Created;
    });

    private Task<TopologyCreationStatus> TryCreateSubscription(string path, string subscriptionName, Action<CreateSubscriptionOptions> action) => SwallowExceptionIfEntityExists(async () =>
    {
        if (await adminClient.SubscriptionExistsAsync(path, subscriptionName)) return TopologyCreationStatus.Exists;

        if (!providerSettings.TopologyProvisioning.CanConsumerCreateSubscription)
        {
            logger.LogWarning("Subscription {SubscriptionName} does not exist on topic {Path} and subscription creation was not allowed", subscriptionName, path);
            return TopologyCreationStatus.NotExists;
        }

        var options = new CreateSubscriptionOptions(path, subscriptionName);
        providerSettings.TopologyProvisioning?.CreateSubscriptionOptions?.Invoke(options);
        action?.Invoke(options);

        logger.LogInformation("Creating subscription: {SubscriptionName} on topic: {Path} ...", subscriptionName, path);
        await adminClient.CreateSubscriptionAsync(options);
        return TopologyCreationStatus.Exists | TopologyCreationStatus.Created;
    });

    private Task<TopologyCreationStatus> TryCreateRule(string path, string subscriptionName, string ruleName, Action<CreateRuleOptions> action) => SwallowExceptionIfEntityExists(async () =>
    {
        if (await adminClient.RuleExistsAsync(path, subscriptionName, ruleName)) return TopologyCreationStatus.Exists;

        if (!providerSettings.TopologyProvisioning.CanConsumerCreateSubscriptionFilter)
        {
            logger.LogWarning("Rule {RuleName} does not exsist on subscription {SubscriptionName} on topic {Path} and filter creation was not allowed", ruleName, subscriptionName, path);
            return TopologyCreationStatus.NotExists;
        }

        var options = new CreateRuleOptions(ruleName);
        providerSettings.TopologyProvisioning?.CreateSubscriptionFilterOptions?.Invoke(options);
        action?.Invoke(options);

        logger.LogInformation("Creating rule: {RuleName} on subscription {SubscriptionName} on topic: {Path} ...", ruleName, subscriptionName, path);
        await adminClient.CreateRuleAsync(path, subscriptionName, options);

        return TopologyCreationStatus.Exists | TopologyCreationStatus.Created;
    });

    public async Task ProvisionTopology()
    {
        try
        {
            logger.LogInformation("Topology provisioning started...");

            var topologyProvisioning = providerSettings.TopologyProvisioning;

            var consumersSettingsByPath = settings.Consumers.OfType<AbstractConsumerSettings>()
                .Concat(new[] { settings.RequestResponse })
                .Where(x => x != null)
                .GroupBy(x => (x.Path, x.PathKind))
                .ToDictionary(x => x.Key, x => x.ToList());

            foreach (var ((path, pathKind), consumerSettingsList) in consumersSettingsByPath)
            {
                if (pathKind == PathKind.Queue)
                {
                    await TryCreateQueue(path, topologyProvisioning.CanConsumerCreateQueue, options =>
                    {
                        foreach (var consumerSettings in consumerSettingsList)
                        {
                            // Note: Populate the require session flag on the queue
                            options.RequiresSession = consumerSettings.GetEnableSession();

                            consumerSettings.GetQueueOptions()?.Invoke(options);
                        }
                    });
                }
                if (pathKind == PathKind.Topic)
                {
                    var topicStatus = await TryCreateTopic(path, topologyProvisioning.CanConsumerCreateTopic, options =>
                    {
                        foreach (var consumerSettings in consumerSettingsList)
                        {
                            consumerSettings.GetTopicOptions()?.Invoke(options);
                        }
                    });

                    if ((topicStatus & TopologyCreationStatus.Exists) != 0)
                    {
                        var consumerSettingsBySubscription = consumerSettingsList
                            .Select(x => (ConsumerSettings: x, SubscriptionName: x.GetSubscriptionName(required: false)))
                            .Where(x => x.SubscriptionName != null)
                            .ToDictionary(x => x.SubscriptionName, x => x.ConsumerSettings);

                        foreach (var (subscriptionName, consumerSettings) in consumerSettingsBySubscription)
                        {
                            var subscriptionStatus = await TryCreateSubscription(path, subscriptionName, options =>
                            {
                                // Note: Populate the require session flag on the subscription
                                options.RequiresSession = consumerSettings.GetEnableSession();

                                consumerSettings.GetSubscriptionOptions()?.Invoke(options);
                            });

                            if ((subscriptionStatus & TopologyCreationStatus.Exists) != 0)
                            {
                                var filters = consumerSettings.GetRules()?.Values;
                                if (filters != null && filters.Count > 0)
                                {
                                    if ((subscriptionStatus & TopologyCreationStatus.Created) != 0 && topologyProvisioning.CanConsumerCreateSubscriptionFilter)
                                    {
                                        // Note: for a newly created subscription, ASB creates a default filter automatically, we need to remove it and let the user defined rules take over
                                        await adminClient.DeleteRuleAsync(path, subscriptionName, "$Default");
                                    }

                                    if (topologyProvisioning.CanConsumerReplaceSubscriptionFilters)
                                    {
                                        // Note: remove already defined rules for existing subscription
                                        var removeRuleTasks = new List<Task<Response>>();
                                        await foreach (var rulesPage in adminClient.GetRulesAsync(path, subscriptionName).AsPages())
                                        {
                                            removeRuleTasks
                                                .AddRange(rulesPage.Values.Where(rule => !filters.Any(filter => filter.Name == rule.Name))
                                                .Select(rule => adminClient.DeleteRuleAsync(path, subscriptionName, rule.Name)));
                                        }
                                        await Task.WhenAll(removeRuleTasks);
                                    }

                                    var tasks = filters.Select(filter => TryCreateRule(path, subscriptionName, filter.Name, options =>
                                    {
                                        options.Filter = new SqlRuleFilter(filter.SqlFilter);
                                        if (filter.SqlAction != null)
                                        {
                                            options.Action = new SqlRuleAction(filter.SqlAction);
                                        }
                                    }));
                                    await Task.WhenAll(tasks);
                                }
                            }
                        }
                    }
                }
            }

            foreach (var producerSettings in settings.Producers)
            {
                if (producerSettings.PathKind == PathKind.Queue)
                {
                    await TryCreateQueue(producerSettings.DefaultPath, topologyProvisioning.CanProducerCreateQueue, options => producerSettings.GetQueueOptions()?.Invoke(options));
                }
                if (producerSettings.PathKind == PathKind.Topic)
                {
                    await TryCreateTopic(producerSettings.DefaultPath, topologyProvisioning.CanProducerCreateTopic, options => producerSettings.GetTopicOptions()?.Invoke(options));
                }
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Could not provision Azure Service Bus topology");
        }
        finally
        {
            logger.LogInformation("Topology provisioning finished");
        }
    }
}