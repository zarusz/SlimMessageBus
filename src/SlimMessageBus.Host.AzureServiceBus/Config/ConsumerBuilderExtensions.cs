namespace SlimMessageBus.Host.AzureServiceBus;

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

public static class ConsumerBuilderExtensions
{
    public static ConsumerBuilder<T> Queue<T>(this ConsumerBuilder<T> builder, string queue)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));

        builder.Path(queue);
        builder.ConsumerSettings.PathKind = PathKind.Queue;
        return builder;
    }

    public static ConsumerBuilder<T> Queue<T>(this ConsumerBuilder<T> builder, string queue, Action<ConsumerBuilder<T>> topicConfig)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (topicConfig is null) throw new ArgumentNullException(nameof(topicConfig));

        var b = builder.Queue(queue);
        topicConfig(b);
        return b;
    }

    /// <summary>
    /// Azure Service Bus consumer setting. See underlying client for more details: https://docs.microsoft.com/en-us/dotnet/api/azure.messaging.servicebus.servicebusprocessoroptions.maxautolockrenewalduration
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder"></param>
    /// <param name="duration"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static ConsumerBuilder<T> MaxAutoLockRenewalDuration<T>(this ConsumerBuilder<T> builder, TimeSpan duration)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));

        builder.ConsumerSettings.SetMaxAutoLockRenewalDuration(duration);

        return builder;
    }

    /// <summary>
    /// Azure Service Bus consumer setting. See underlying client for more details: https://docs.microsoft.com/en-us/dotnet/api/azure.messaging.servicebus.servicebusprocessoroptions.subqueue
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder"></param>
    /// <param name="duration"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static ConsumerBuilder<T> SubQueue<T>(this ConsumerBuilder<T> builder, SubQueue subQueue)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));

        builder.ConsumerSettings.SetSubQueue(subQueue);

        return builder;
    }

    /// <summary>
    /// Azure Service Bus consumer setting. See underlying client for more details: https://docs.microsoft.com/en-us/dotnet/api/azure.messaging.servicebus.servicebusprocessoroptions.prefetchcount
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder"></param>
    /// <param name="prefetchCount "></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static ConsumerBuilder<T> PrefetchCount<T>(this ConsumerBuilder<T> builder, int prefetchCount)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));

        builder.ConsumerSettings.SetPrefetchCount(prefetchCount);

        return builder;
    }

    /// <summary>
    /// Enables Azue Service Bus session support for this consumer
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static ConsumerBuilder<T> EnableSession<T>(this ConsumerBuilder<T> builder, Action<ConsumerSessionBuilder> sessionConfiguration = null)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));

        builder.ConsumerSettings.SetEnableSession(true);
        if (sessionConfiguration != null)
        {
            sessionConfiguration(new ConsumerSessionBuilder(builder.ConsumerSettings));
        }

        return builder;
    }

    /// <summary>
    /// Adds a named SQL filter to the subscription (Azure Service Bus). Setting relevant only if topology provisioning enabled.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder"></param>
    /// <param name="ruleName">The name of the filter</param>
    /// <param name="filterSql">The SQL expression of the filter</param>
    /// <param name="actionSql">The action to be performed on the filter</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static ConsumerBuilder<T> SubscriptionSqlFilter<T>(this ConsumerBuilder<T> builder, string filterSql, string ruleName = "default", string actionSql = null)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));

        var filterByName = builder.ConsumerSettings.GetRules(createIfNotExists: true);
        filterByName[ruleName] = new SubscriptionSqlRule { Name = ruleName, SqlFilter = filterSql, SqlAction = actionSql };

        return builder;
    }

    /// <summary>
    /// <see cref="CreateQueueOptions"/> when the ASB queue does not exist and needs to be created
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static T CreateQueueOptions<T>(this T builder, Action<CreateQueueOptions> action) where T : AbstractConsumerBuilder
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (action is null) throw new ArgumentNullException(nameof(action));

        builder.ConsumerSettings.SetQueueOptions(action);
        return builder;
    }

    /// <summary>
    /// <see cref="CreateTopicOptions"/> when the ASB topic does not exist and needs to be created
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static T CreateTopicOptions<T>(this T builder, Action<CreateTopicOptions> action) where T : AbstractConsumerBuilder
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (action is null) throw new ArgumentNullException(nameof(action));

        builder.ConsumerSettings.SetTopicOptions(action);
        return builder;
    }

    /// <summary>
    /// <see cref="CreateSubscriptionOptions"/> when the ASB subscription does not exist and needs to be created
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static T CreateSubscriptionOptions<T>(this T builder, Action<CreateSubscriptionOptions> action) where T : AbstractConsumerBuilder
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (action is null) throw new ArgumentNullException(nameof(action));

        builder.ConsumerSettings.SetSubscriptionOptions(action);
        return builder;
    }

    private const string MaxAutoLockRenewalDurationKey = "Asb_MaxAutoLockRenewalDuration";
    private const string SubQueueKey = "Asb_SubQueue";
    private const string PrefetchCountKey = "Asb_PrefetchCount";
    private const string EnableSessionKey = "Asb_SessionEnabled";
    private const string SessionIdleTimeoutKey = "Asb_SessionIdleTimeout";
    private const string MaxConcurrentSessionsKey = "Asb_MaxConcurrentSessions";
    private const string RulesKey = "Asb_Rules";

    internal static void SetMaxAutoLockRenewalDuration(this AbstractConsumerSettings consumerSettings, TimeSpan duration)
        => consumerSettings.Properties[MaxAutoLockRenewalDurationKey] = duration;

    internal static TimeSpan? GetMaxAutoLockRenewalDuration(this AbstractConsumerSettings consumerSettings)
        => consumerSettings.GetOrDefault<TimeSpan?>(MaxAutoLockRenewalDurationKey);

    internal static void SetSubQueue(this AbstractConsumerSettings consumerSettings, SubQueue subQueue)
        => consumerSettings.Properties[SubQueueKey] = subQueue;

    internal static SubQueue? GetSubQueue(this AbstractConsumerSettings consumerSettings)
        => consumerSettings.GetOrDefault<SubQueue?>(SubQueueKey);

    internal static void SetPrefetchCount(this AbstractConsumerSettings consumerSettings, int prefetchCount)
        => consumerSettings.Properties[PrefetchCountKey] = prefetchCount;

    internal static int? GetPrefetchCount(this AbstractConsumerSettings consumerSettings)
        => consumerSettings.GetOrDefault<int?>(PrefetchCountKey);

    internal static void SetEnableSession(this AbstractConsumerSettings consumerSettings, bool enableSession)
        => consumerSettings.Properties[EnableSessionKey] = enableSession;

    internal static bool GetEnableSession(this AbstractConsumerSettings consumerSettings)
        => consumerSettings.GetOrDefault(EnableSessionKey, false);

    internal static void SetSessionIdleTimeout(this AbstractConsumerSettings consumerSettings, TimeSpan sessionIdleTimeout)
        => consumerSettings.Properties[SessionIdleTimeoutKey] = sessionIdleTimeout;

    internal static TimeSpan? GetSessionIdleTimeout(this AbstractConsumerSettings consumerSettings)
        => consumerSettings.GetOrDefault<TimeSpan?>(SessionIdleTimeoutKey);

    internal static void SetMaxConcurrentSessions(this AbstractConsumerSettings consumerSettings, int maxConcurrentSessions)
        => consumerSettings.Properties[MaxConcurrentSessionsKey] = maxConcurrentSessions;

    internal static int? GetMaxConcurrentSessions(this AbstractConsumerSettings consumerSettings)
        => consumerSettings.GetOrDefault<int?>(MaxConcurrentSessionsKey);

    internal static IDictionary<string, SubscriptionSqlRule> GetRules(this AbstractConsumerSettings consumerSettings, bool createIfNotExists = false)
    {
        var filterByName = consumerSettings.GetOrDefault<IDictionary<string, SubscriptionSqlRule>>(RulesKey);
        if (filterByName == null && createIfNotExists)
        {
            filterByName = new Dictionary<string, SubscriptionSqlRule>();
            consumerSettings.Properties[RulesKey] = filterByName;
        }
        return filterByName;
    }
}