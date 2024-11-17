namespace SlimMessageBus.Host.AzureServiceBus;

using System;
using System.Data;

public class ServiceBusTopologyService
{
    private readonly ILogger<ServiceBusTopologyService> _logger;
    private readonly MessageBusSettings _settings;
    private readonly ServiceBusMessageBusSettings _providerSettings;
    private readonly ServiceBusAdministrationClient _adminClient;

    public ServiceBusTopologyService(ILogger<ServiceBusTopologyService> logger, MessageBusSettings settings, ServiceBusMessageBusSettings providerSettings)
    {
        _logger = logger;
        _settings = settings;
        _providerSettings = providerSettings;
        _adminClient = providerSettings.AdminClientFactory(settings.ServiceProvider, providerSettings);
    }

    [Flags]
    private enum TopologyCreationStatus
    {
        None = 0,
        NotExists = 1,
        Exists = 2,
        Created = 4,
        Updated = 8
    }

    private static async Task<TopologyCreationStatus> SwallowExceptionIfEntityExists(Func<Task<TopologyCreationStatus>> task)
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

    private static async Task<T> SwallowExceptionIfMessagingEntityNotFound<T>(Func<Task<T>> task)
    {
        try
        {
            return await task();
        }
        catch (ServiceBusException e) when (e.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
            // do nothing as another service instance might have deleted entity in the meantime
            return default;
        }
    }

    private Task<TopologyCreationStatus> TryCreateQueue(string path, bool canCreate, Action<CreateQueueOptions> action) => SwallowExceptionIfEntityExists(async () =>
    {
        if (await _adminClient.QueueExistsAsync(path)) return TopologyCreationStatus.Exists;

        if (!canCreate)
        {
            _logger.LogWarning("Queue {Path} does not exist and queue creation was not allowed", path);
            return TopologyCreationStatus.NotExists;
        }

        var options = new CreateQueueOptions(path);
        _providerSettings.TopologyProvisioning?.CreateQueueOptions?.Invoke(options);
        action?.Invoke(options);

        _logger.LogInformation("Creating queue: {Path} ...", path);
        await _adminClient.CreateQueueAsync(options);

        return TopologyCreationStatus.Exists | TopologyCreationStatus.Created;
    });

    private Task<TopologyCreationStatus> TryCreateTopic(string path, bool canCreate, Action<CreateTopicOptions> action) => SwallowExceptionIfEntityExists(async () =>
    {
        if (await _adminClient.TopicExistsAsync(path)) return TopologyCreationStatus.Exists;

        if (!canCreate)
        {
            _logger.LogWarning("Topic {Path} does not exist and topic creation was not allowed", path);
            return TopologyCreationStatus.NotExists;
        }

        var options = new CreateTopicOptions(path);
        _providerSettings.TopologyProvisioning?.CreateTopicOptions?.Invoke(options);
        action?.Invoke(options);

        _logger.LogInformation("Creating topic: {Path} ...", path);
        await _adminClient.CreateTopicAsync(options);

        return TopologyCreationStatus.Exists | TopologyCreationStatus.Created;
    });

    private Task<TopologyCreationStatus> TryCreateSubscription(string path, string subscriptionName, Func<CreateSubscriptionOptions> optionsFactory) => SwallowExceptionIfEntityExists(async () =>
    {
        if (await _adminClient.SubscriptionExistsAsync(path, subscriptionName)) return TopologyCreationStatus.Exists;

        if (!_providerSettings.TopologyProvisioning.CanConsumerCreateSubscription)
        {
            _logger.LogWarning("Subscription {SubscriptionName} does not exist on topic {Path} and subscription creation was not allowed", subscriptionName, path);
            return TopologyCreationStatus.NotExists;
        }

        var options = optionsFactory();

        _logger.LogInformation("Creating subscription: {SubscriptionName} on topic: {Path} ...", subscriptionName, path);
        await _adminClient.CreateSubscriptionAsync(options);
        return TopologyCreationStatus.Exists | TopologyCreationStatus.Created;
    });

    private Task<TopologyCreationStatus> TryCreateRule(string path, string subscriptionName, CreateRuleOptions options) => SwallowExceptionIfEntityExists(async () =>
    {
        if (!_providerSettings.TopologyProvisioning.CanConsumerCreateSubscriptionFilter)
        {
            _logger.LogWarning("Rule {RuleName} does not exist on subscription {SubscriptionName} on topic {Path} and options creation was not allowed", options.Name, subscriptionName, path);
            return TopologyCreationStatus.NotExists;
        }

        _logger.LogInformation("Creating options: {RuleName} on subscription {SubscriptionName} on topic: {Path} ...", options.Name, subscriptionName, path);
        await _adminClient.CreateRuleAsync(path, subscriptionName, options);

        return TopologyCreationStatus.Exists | TopologyCreationStatus.Created;
    });

    private Task<TopologyCreationStatus> TryDeleteRule(string path, string subscriptionName, string name) => SwallowExceptionIfMessagingEntityNotFound(async () =>
    {
        if (!_providerSettings.TopologyProvisioning.CanConsumerReplaceSubscriptionFilters)
        {
            _logger.LogWarning("Rule {RuleName} exists on subscription {SubscriptionName} on topic {Path} but should not. Updating options is not allowed.", name, subscriptionName, path);
            return TopologyCreationStatus.Exists;
        }

        _logger.LogInformation("Replacing options: removing {RuleName} on subscription {SubscriptionName} on topic: {Path} ...", name, subscriptionName, path);
        await _adminClient.DeleteRuleAsync(path, subscriptionName, name);

        return TopologyCreationStatus.Exists | TopologyCreationStatus.Created;
    });

    private Task<TopologyCreationStatus> TryUpdateRule(string path, string subscriptionName, RuleProperties options) => SwallowExceptionIfEntityExists(async () =>
    {
        if (!_providerSettings.TopologyProvisioning.CanConsumerReplaceSubscriptionFilters)
        {
            _logger.LogWarning("Rule {RuleName} exists on subscription {SubscriptionName} on topic {Path} but does not match the expected configuration. Updating options is not allowed.", options.Name, subscriptionName, path);
            return TopologyCreationStatus.NotExists;
        }

        _logger.LogInformation("Updating options: {RuleName} on subscription {SubscriptionName} on topic: {Path} ...", options.Name, subscriptionName, path);
        await _adminClient.UpdateRuleAsync(path, subscriptionName, options);

        return TopologyCreationStatus.Exists | TopologyCreationStatus.Updated;
    });

    public Task ProvisionTopology() => _providerSettings.TopologyProvisioning.OnProvisionTopology(_adminClient, DoProvisionTopology);

    protected async Task DoProvisionTopology()
    {
        try
        {
            _logger.LogInformation("Topology provisioning started...");

            var topologyProvisioning = _providerSettings.TopologyProvisioning;

            var consumersSettingsByPath = _settings.Consumers.OfType<AbstractConsumerSettings>()
                .Concat(new[] { _settings.RequestResponse })
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
                            .Select(x => (ConsumerSettings: x, SubscriptionName: x.GetSubscriptionName(_providerSettings)))
                            .Where(x => x.SubscriptionName != null)
                            .GroupBy(x => x.SubscriptionName)
                            .ToDictionary(x => x.Key, x => x.Select(z => z.ConsumerSettings).ToList());

                        foreach (var (subscriptionName, consumerSettingsGroup) in consumerSettingsBySubscription)
                        {
                            var subscriptionStatus = await TryCreateSubscription(path, subscriptionName, () =>
                            {
                                void ThrowOnFalse(bool value, string settingName)
                                {
                                    if (value)
                                    {
                                        return;
                                    }

                                    var topicSubscription = new TopicSubscriptionParams(path, subscriptionName);
                                    throw new ConfigurationMessageBusException($"All {nameof(CreateSubscriptionOptions)} instances across the same path/subscription {topicSubscription} must have the same {settingName} settings.");
                                }

                                var options = consumerSettingsGroup.Aggregate((CreateSubscriptionOptions)null, (acc, consumerSettings) =>
                                {
                                    var options = new CreateSubscriptionOptions(path, subscriptionName);
                                    _providerSettings.TopologyProvisioning?.CreateSubscriptionOptions?.Invoke(options);
                                    options.RequiresSession = consumerSettings.GetEnableSession();

                                    consumerSettings.GetSubscriptionOptions()?.Invoke(options);
                                    if (acc != null && !ReferenceEquals(acc, options))
                                    {
                                        ThrowOnFalse(acc.AutoDeleteOnIdle.Equals(options.AutoDeleteOnIdle), nameof(options.AutoDeleteOnIdle));
                                        ThrowOnFalse(acc.DefaultMessageTimeToLive.Equals(options.DefaultMessageTimeToLive), nameof(options.DefaultMessageTimeToLive));
                                        ThrowOnFalse(acc.EnableBatchedOperations == options.EnableBatchedOperations, nameof(options.EnableBatchedOperations));
                                        ThrowOnFalse(acc.DeadLetteringOnMessageExpiration == options.DeadLetteringOnMessageExpiration, nameof(options.DeadLetteringOnMessageExpiration));
                                        ThrowOnFalse(acc.EnableDeadLetteringOnFilterEvaluationExceptions == options.EnableDeadLetteringOnFilterEvaluationExceptions, nameof(options.EnableDeadLetteringOnFilterEvaluationExceptions));
                                        ThrowOnFalse(string.Equals(acc.ForwardDeadLetteredMessagesTo, options.ForwardDeadLetteredMessagesTo, StringComparison.OrdinalIgnoreCase), nameof(options.ForwardDeadLetteredMessagesTo));
                                        ThrowOnFalse(string.Equals(acc.ForwardTo, options.ForwardTo, StringComparison.OrdinalIgnoreCase), nameof(options.ForwardTo));
                                        ThrowOnFalse(acc.LockDuration.Equals(options.LockDuration), nameof(options.LockDuration));
                                        ThrowOnFalse(acc.MaxDeliveryCount == options.MaxDeliveryCount, nameof(options.MaxDeliveryCount));
                                        ThrowOnFalse(acc.RequiresSession.Equals(options.RequiresSession), nameof(options.RequiresSession));
                                        ThrowOnFalse(acc.Status.Equals(options.Status), nameof(options.Status));
                                        ThrowOnFalse(string.Equals(acc.UserMetadata, options.UserMetadata, StringComparison.OrdinalIgnoreCase), nameof(options.UserMetadata));
                                    }

                                    return options;
                                });

                                return options;
                            });

                            if ((subscriptionStatus & TopologyCreationStatus.Exists) != 0 && (topologyProvisioning.CanConsumerValidateSubscriptionFilters || topologyProvisioning.CanConsumerCreateSubscriptionFilter || topologyProvisioning.CanConsumerReplaceSubscriptionFilters))
                            {
                                var tasks = new List<Task>();
                                var rules = MergeFilters(path, subscriptionName, consumerSettingsGroup).ToDictionary(x => x.Name, x => x);

                                await foreach (var page in _adminClient.GetRulesAsync(path, subscriptionName).AsPages())
                                {
                                    foreach (var serviceRule in page.Values)
                                    {
                                        if (!rules.TryGetValue(serviceRule.Name, out var rule))
                                        {
                                            // server rule was not defined in SMB
                                            if ((rules.Count > 0 || serviceRule.Name != RuleProperties.DefaultRuleName) && ((subscriptionStatus & TopologyCreationStatus.Created) != 0 || topologyProvisioning.CanConsumerReplaceSubscriptionFilters))
                                            {
                                                // Note: for a newly created subscription, ASB creates a $Default filter automatically
                                                // We need to remove the filter if its not matching what is declared in SMB and let the user defined rules take over
                                                // On the other hand if there are no user defined rules, we need to preserve the $Default filter
                                                tasks.Add(TryDeleteRule(path, subscriptionName, serviceRule.Name));
                                            }
                                            continue;
                                        }

                                        if (rule.Filter.Equals(serviceRule.Filter) && ((rule.Action == null && serviceRule.Action == null) || (rule.Action.Equals(serviceRule.Action))))
                                        {
                                            // server rule matched what is defined in SMB
                                            rules.Remove(serviceRule.Name);
                                            continue;
                                        }

                                        if (topologyProvisioning.CanConsumerReplaceSubscriptionFilters)
                                        {
                                            // update existing rule
                                            serviceRule.Filter = rule.Filter;
                                            serviceRule.Action = rule.Action;
                                            tasks.Add(TryUpdateRule(path, subscriptionName, serviceRule));
                                            rules.Remove(serviceRule.Name);
                                        }
                                    }

                                    if (topologyProvisioning.CanConsumerCreateSubscriptionFilter)
                                    {
                                        tasks.AddRange(rules.Values.Select(options => TryCreateRule(path, subscriptionName, options)));
                                    }
                                }
                                await Task.WhenAll(tasks);
                            }
                        }
                    }
                }
            }

            foreach (var producerSettings in _settings.Producers)
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
            _logger.LogError(e, "Could not provision Azure Service Bus topology");
        }
        finally
        {
            _logger.LogInformation("Topology provisioning finished");
        }
    }

    IReadOnlyCollection<CreateRuleOptions> MergeFilters(string path, string subscriptionName, IList<AbstractConsumerSettings> settings)
    {
        var rules = settings.SelectMany(x => x.GetRules()?.Values ?? []).GroupBy(x => x).ToList();
        var dict = new Dictionary<string, CreateRuleOptions>(rules.Count);
        foreach (var rule in rules.Select(x => x.Key))
        {
            var name = rule.Name;
            if (dict.ContainsKey(name))
            {
                throw new ConfigurationMessageBusException($"All rules across the same path/subscription {path}/{subscriptionName} must have unique names (Duplicate: '{name}').");
            }

            var createRuleOptions = new CreateRuleOptions(name)
            {
                Filter = new SqlRuleFilter(rule.SqlFilter),
                Action = !string.IsNullOrWhiteSpace(rule.SqlAction) ? new SqlRuleAction(rule.SqlAction) : null
            };

            _providerSettings.TopologyProvisioning?.CreateSubscriptionFilterOptions?.Invoke(createRuleOptions);

            dict.Add(name, createRuleOptions);
        }

        return dict.Values;
    }
}