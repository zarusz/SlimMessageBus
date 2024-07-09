namespace SlimMessageBus.Host.AzureServiceBus;

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

public static class AsbConsumerBuilderExtensions
{
    /// <summary>
    /// Sets the queue name for this consumer to use.
    /// </summary>
    /// <typeparam name="TConsumerBuilder"></typeparam>
    /// <param name="builder"></param>
    /// <param name="queue"></param>
    /// <param name="queueConfig"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static TConsumerBuilder Queue<TConsumerBuilder>(this TConsumerBuilder builder, string queue, Action<TConsumerBuilder> queueConfig = null)
        where TConsumerBuilder : AbstractConsumerBuilder
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (queue is null) throw new ArgumentNullException(nameof(queue));

        builder.ConsumerSettings.Path = queue;
        builder.ConsumerSettings.PathKind = PathKind.Queue;

        queueConfig?.Invoke(builder);

        return builder;
    }

    private static void AssertIsTopicForSubscriptionName(AbstractConsumerSettings settings)
    {
        if (settings is null) throw new ArgumentNullException(nameof(settings));

        if (settings.PathKind == PathKind.Queue)
        {
            var methodName = $".{nameof(SubscriptionName)}(...)";

            var messageType = settings is ConsumerSettings consumerSettings
                ? consumerSettings.MessageType.FullName
                : string.Empty;

            throw new ConfigurationMessageBusException($"The subscription name configuration ({methodName}) does not apply to Azure ServiceBus queues (it only applies to topic consumers). Remove the {methodName} configuration for type {messageType} and queue {settings.Path} or change the consumer configuration to consume from topic {settings.Path} instead.");
        }
    }

    /// <summary>
    /// Configures the subscription name when consuming form Azure ServiceBus topic.
    /// Not applicable when consuming from Azure ServiceBus queue.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="subscriptionName"></param>
    /// <returns></returns>
    public static T SubscriptionName<T>(this T builder, string subscriptionName)
        where T : IAbstractConsumerBuilder
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (subscriptionName is null) throw new ArgumentNullException(nameof(subscriptionName));

        AssertIsTopicForSubscriptionName(builder.ConsumerSettings);

        builder.ConsumerSettings.SetSubscriptionName(subscriptionName);
        return builder;
    }

    /// <summary>
    /// Azure Service Bus consumer setting. See underlying client for more details: https://docs.microsoft.com/en-us/dotnet/api/azure.messaging.servicebus.servicebusprocessoroptions.maxautolockrenewalduration
    /// </summary>
    /// <typeparam name="TConsumerBuilder"></typeparam>
    /// <param name="builder"></param>
    /// <param name="duration"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static TConsumerBuilder MaxAutoLockRenewalDuration<TConsumerBuilder>(this TConsumerBuilder builder, TimeSpan duration)
        where TConsumerBuilder : IAbstractConsumerBuilder
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));

        builder.ConsumerSettings.SetMaxAutoLockRenewalDuration(duration);

        return builder;
    }

    /// <summary>
    /// Azure Service Bus consumer setting. See underlying client for more details: https://docs.microsoft.com/en-us/dotnet/api/azure.messaging.servicebus.servicebusprocessoroptions.subqueue
    /// </summary>
    /// <typeparam name="TConsumerBuilder"></typeparam>
    /// <param name="builder"></param>
    /// <param name="subQueue"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static TConsumerBuilder SubQueue<TConsumerBuilder>(this TConsumerBuilder builder, SubQueue subQueue)
        where TConsumerBuilder : IAbstractConsumerBuilder
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));

        builder.ConsumerSettings.SetSubQueue(subQueue);

        return builder;
    }

    /// <summary>
    /// Azure Service Bus consumer setting. See underlying client for more details: https://docs.microsoft.com/en-us/dotnet/api/azure.messaging.servicebus.servicebusprocessoroptions.prefetchcount
    /// </summary>
    /// <typeparam name="TConsumerBuilder"></typeparam>
    /// <param name="builder"></param>
    /// <param name="prefetchCount "></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static TConsumerBuilder PrefetchCount<TConsumerBuilder>(this TConsumerBuilder builder, int prefetchCount)
        where TConsumerBuilder : IAbstractConsumerBuilder
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));

        builder.ConsumerSettings.SetPrefetchCount(prefetchCount);

        return builder;
    }

    /// <summary>
    /// Enables Azure Service Bus session support for this consumer
    /// </summary>
    /// <typeparam name="TConsumerBuilder"></typeparam>
    /// <param name="builder"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static TConsumerBuilder EnableSession<TConsumerBuilder>(this TConsumerBuilder builder, Action<AsbConsumerSessionBuilder> sessionConfiguration = null)
        where TConsumerBuilder : IAbstractConsumerBuilder
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));

        builder.ConsumerSettings.SetEnableSession(true);

        if (sessionConfiguration != null)
        {
            sessionConfiguration(new AsbConsumerSessionBuilder(builder.ConsumerSettings));
        }

        return builder;
    }

    /// <summary>
    /// Adds a named SQL filter to the subscription (Azure Service Bus). Setting relevant only if topology provisioning enabled.
    /// </summary>
    /// <typeparam name="TConsumerBuilder"></typeparam>
    /// <param name="builder"></param>
    /// <param name="ruleName">The name of the filter</param>
    /// <param name="filterSql">The SQL expression of the filter</param>
    /// <param name="actionSql">The action to be performed on the filter</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static TConsumerBuilder SubscriptionSqlFilter<TConsumerBuilder>(this TConsumerBuilder builder, string filterSql, string ruleName = "default", string actionSql = null)
        where TConsumerBuilder : IAbstractConsumerBuilder
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));

        var filterByName = builder.ConsumerSettings.GetRules(createIfNotExists: true);
        filterByName[ruleName] = new SubscriptionSqlRule { Name = ruleName, SqlFilter = filterSql, SqlAction = actionSql };

        return builder;
    }

    /// <summary>
    /// <see cref="CreateQueueOptions"/> when the ASB queue does not exist and needs to be created
    /// </summary>
    /// <typeparam name="TConsumerBuilder"></typeparam>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static TConsumerBuilder CreateQueueOptions<TConsumerBuilder>(this TConsumerBuilder builder, Action<CreateQueueOptions> action)
        where TConsumerBuilder : IAbstractConsumerBuilder
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (action is null) throw new ArgumentNullException(nameof(action));

        builder.ConsumerSettings.SetQueueOptions(action);
        return builder;
    }

    /// <summary>
    /// <see cref="CreateTopicOptions"/> when the ASB topic does not exist and needs to be created
    /// </summary>
    /// <typeparam name="TConsumerBuilder"></typeparam>
    /// <param name="builder"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    public static TConsumerBuilder CreateTopicOptions<TConsumerBuilder>(this TConsumerBuilder builder, Action<CreateTopicOptions> action)
        where TConsumerBuilder : IAbstractConsumerBuilder
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (action is null) throw new ArgumentNullException(nameof(action));

        builder.ConsumerSettings.SetTopicOptions(action);
        return builder;
    }

    /// <summary>
    /// <see cref="CreateSubscriptionOptions"/> when the ASB subscription does not exist and needs to be created
    /// </summary>
    /// <typeparam name="TConsumerBuilder"></typeparam>
    /// <param name="builder"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    public static TConsumerBuilder CreateSubscriptionOptions<TConsumerBuilder>(this TConsumerBuilder builder, Action<CreateSubscriptionOptions> action)
        where TConsumerBuilder : IAbstractConsumerBuilder
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (action is null) throw new ArgumentNullException(nameof(action));

        builder.ConsumerSettings.SetSubscriptionOptions(action);
        return builder;
    }
}