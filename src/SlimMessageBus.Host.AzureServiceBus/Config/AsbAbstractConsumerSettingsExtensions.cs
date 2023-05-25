namespace SlimMessageBus.Host.AzureServiceBus;

public static class AsbAbstractConsumerSettingsExtensions
{
    internal static void SetSubscriptionName(this AbstractConsumerSettings consumerSettings, string subscriptionName)
    {
        if (subscriptionName is null) throw new ArgumentNullException(nameof(subscriptionName));

        consumerSettings.Properties[AsbProperties.SubscriptionNameKey] = subscriptionName;
    }

    internal static string GetSubscriptionName(this AbstractConsumerSettings consumerSettings, bool required = true)
    {
        if (!consumerSettings.Properties.ContainsKey(AsbProperties.SubscriptionNameKey) && !required)
        {
            return null;
        }
        return consumerSettings.Properties[AsbProperties.SubscriptionNameKey] as string;
    }

    internal static void SetMaxAutoLockRenewalDuration(this AbstractConsumerSettings consumerSettings, TimeSpan duration)
        => consumerSettings.Properties[AsbProperties.MaxAutoLockRenewalDurationKey] = duration;

    internal static TimeSpan? GetMaxAutoLockRenewalDuration(this AbstractConsumerSettings consumerSettings)
        => consumerSettings.GetOrDefault<TimeSpan?>(AsbProperties.MaxAutoLockRenewalDurationKey);

    internal static void SetSubQueue(this AbstractConsumerSettings consumerSettings, SubQueue subQueue)
        => consumerSettings.Properties[AsbProperties.SubQueueKey] = subQueue;

    internal static SubQueue? GetSubQueue(this AbstractConsumerSettings consumerSettings)
        => consumerSettings.GetOrDefault<SubQueue?>(AsbProperties.SubQueueKey);

    internal static void SetPrefetchCount(this AbstractConsumerSettings consumerSettings, int prefetchCount)
        => consumerSettings.Properties[AsbProperties.PrefetchCountKey] = prefetchCount;

    internal static int? GetPrefetchCount(this AbstractConsumerSettings consumerSettings)
        => consumerSettings.GetOrDefault<int?>(AsbProperties.PrefetchCountKey);

    internal static void SetEnableSession(this AbstractConsumerSettings consumerSettings, bool enableSession)
        => consumerSettings.Properties[AsbProperties.EnableSessionKey] = enableSession;

    internal static bool GetEnableSession(this AbstractConsumerSettings consumerSettings)
        => consumerSettings.GetOrDefault(AsbProperties.EnableSessionKey, false);

    internal static void SetSessionIdleTimeout(this AbstractConsumerSettings consumerSettings, TimeSpan sessionIdleTimeout)
        => consumerSettings.Properties[AsbProperties.SessionIdleTimeoutKey] = sessionIdleTimeout;

    internal static TimeSpan? GetSessionIdleTimeout(this AbstractConsumerSettings consumerSettings)
        => consumerSettings.GetOrDefault<TimeSpan?>(AsbProperties.SessionIdleTimeoutKey);

    internal static void SetMaxConcurrentSessions(this AbstractConsumerSettings consumerSettings, int maxConcurrentSessions)
        => consumerSettings.Properties[AsbProperties.MaxConcurrentSessionsKey] = maxConcurrentSessions;

    internal static int? GetMaxConcurrentSessions(this AbstractConsumerSettings consumerSettings)
        => consumerSettings.GetOrDefault<int?>(AsbProperties.MaxConcurrentSessionsKey);

    internal static IDictionary<string, SubscriptionSqlRule> GetRules(this AbstractConsumerSettings consumerSettings, bool createIfNotExists = false)
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
