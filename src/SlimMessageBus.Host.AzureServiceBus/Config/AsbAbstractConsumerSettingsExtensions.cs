namespace SlimMessageBus.Host.AzureServiceBus;

public static class AsbAbstractConsumerSettingsExtensions
{
    static internal void SetSubscriptionName(this AbstractConsumerSettings consumerSettings, string subscriptionName)
    {
        if (subscriptionName is null) throw new ArgumentNullException(nameof(subscriptionName));

        consumerSettings.Properties[AsbProperties.SubscriptionNameKey] = subscriptionName;
    }

    static internal string GetSubscriptionName(this AbstractConsumerSettings consumerSettings, bool required = true)
    {
        if (!consumerSettings.Properties.ContainsKey(AsbProperties.SubscriptionNameKey) && !required)
        {
            return null;
        }
        return consumerSettings.Properties[AsbProperties.SubscriptionNameKey] as string;
    }

    static internal void SetMaxAutoLockRenewalDuration(this AbstractConsumerSettings consumerSettings, TimeSpan duration)
        => consumerSettings.Properties[AsbProperties.MaxAutoLockRenewalDurationKey] = duration;

    static internal TimeSpan? GetMaxAutoLockRenewalDuration(this AbstractConsumerSettings consumerSettings)
        => consumerSettings.GetOrDefault<TimeSpan?>(AsbProperties.MaxAutoLockRenewalDurationKey);

    static internal void SetSubQueue(this AbstractConsumerSettings consumerSettings, SubQueue subQueue)
        => consumerSettings.Properties[AsbProperties.SubQueueKey] = subQueue;

    static internal SubQueue? GetSubQueue(this AbstractConsumerSettings consumerSettings)
        => consumerSettings.GetOrDefault<SubQueue?>(AsbProperties.SubQueueKey);

    static internal void SetPrefetchCount(this AbstractConsumerSettings consumerSettings, int prefetchCount)
        => consumerSettings.Properties[AsbProperties.PrefetchCountKey] = prefetchCount;

    static internal int? GetPrefetchCount(this AbstractConsumerSettings consumerSettings)
        => consumerSettings.GetOrDefault<int?>(AsbProperties.PrefetchCountKey);

    static internal void SetEnableSession(this AbstractConsumerSettings consumerSettings, bool enableSession)
        => consumerSettings.Properties[AsbProperties.EnableSessionKey] = enableSession;

    static internal bool GetEnableSession(this AbstractConsumerSettings consumerSettings)
        => consumerSettings.GetOrDefault(AsbProperties.EnableSessionKey, false);

    static internal void SetSessionIdleTimeout(this AbstractConsumerSettings consumerSettings, TimeSpan sessionIdleTimeout)
        => consumerSettings.Properties[AsbProperties.SessionIdleTimeoutKey] = sessionIdleTimeout;

    static internal TimeSpan? GetSessionIdleTimeout(this AbstractConsumerSettings consumerSettings)
        => consumerSettings.GetOrDefault<TimeSpan?>(AsbProperties.SessionIdleTimeoutKey);

    static internal void SetMaxConcurrentSessions(this AbstractConsumerSettings consumerSettings, int maxConcurrentSessions)
        => consumerSettings.Properties[AsbProperties.MaxConcurrentSessionsKey] = maxConcurrentSessions;

    static internal int? GetMaxConcurrentSessions(this AbstractConsumerSettings consumerSettings)
        => consumerSettings.GetOrDefault<int?>(AsbProperties.MaxConcurrentSessionsKey);

    static internal IDictionary<string, SubscriptionSqlRule> GetRules(this AbstractConsumerSettings consumerSettings, bool createIfNotExists = false)
    {
        var filterByName = consumerSettings.GetOrDefault<IDictionary<string, SubscriptionSqlRule>>(AsbProperties.RulesKey);
        if (filterByName == null && createIfNotExists)
        {
            filterByName = new Dictionary<string, SubscriptionSqlRule>();
            consumerSettings.Properties[AsbProperties.RulesKey] = filterByName;
        }
        return filterByName;
    }
}
