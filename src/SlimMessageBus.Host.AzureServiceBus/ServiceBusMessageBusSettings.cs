namespace SlimMessageBus.Host.AzureServiceBus;

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

public class ServiceBusMessageBusSettings : HasProviderExtensions
{
    public string ConnectionString { get; set; }
    public Func<IServiceProvider, ServiceBusMessageBusSettings, ServiceBusClient> ClientFactory { get; set; }
    public Func<IServiceProvider, ServiceBusMessageBusSettings, ServiceBusAdministrationClient> AdminClientFactory { get; set; }
    public Func<string, ServiceBusClient, ServiceBusSender> SenderFactory { get; set; }
    public Func<TopicSubscriptionParams, ServiceBusProcessorOptions> ProcessorOptionsFactory { get; set; }
    public Func<TopicSubscriptionParams, ServiceBusProcessorOptions, ServiceBusClient, ServiceBusProcessor> ProcessorFactory { get; set; }
    public Func<TopicSubscriptionParams, ServiceBusSessionProcessorOptions> SessionProcessorOptionsFactory { get; set; }
    public Func<TopicSubscriptionParams, ServiceBusSessionProcessorOptions, ServiceBusClient, ServiceBusSessionProcessor> SessionProcessorFactory { get; set; }

    /// <summary>
    /// This will be the default value applied on each consumer. Specific consumer may override this value.
    /// See underlying client for more details: https://docs.microsoft.com/en-us/dotnet/api/azure.messaging.servicebus.servicebusprocessoroptions.maxautolockrenewalduration
    /// </summary>
    public TimeSpan? MaxAutoLockRenewalDuration { get; set; }

    /// <summary>
    /// This will be the default value applied on each consumer. Specific consumer may override this value.
    /// See underlying client for more details: https://docs.microsoft.com/en-us/dotnet/api/azure.messaging.servicebus.servicebusprocessoroptions.prefetchcount
    /// </summary>
    public int? PrefetchCount { get; set; }

    /// <summary>
    /// This will be the default value applied on each consumer. Specific consumer may override this value.
    /// See underlying client for more details: https://docs.microsoft.com/en-us/dotnet/api/azure.messaging.servicebus.servicebussessionprocessoroptions.sessionidletimeout
    /// </summary>
    public TimeSpan? SessionIdleTimeout { get; set; }

    /// <summary>
    /// This will be the default value applied on each consumer. Specific consumer may override this value.
    /// See underlying client for more details: https://docs.microsoft.com/en-us/dotnet/api/azure.messaging.servicebus.servicebussessionprocessoroptions.maxconcurrentcallspersession
    /// </summary>
    public int? MaxConcurrentSessions { get; set; }

    /// <summary>
    /// Settings for auto creation of queues, topics, subscriptions and rules if they don't exist.
    /// </summary>
    public ServiceBusTopologySettings TopologyProvisioning { get; set; }

    public ServiceBusMessageBusSettings()
    {
        ClientFactory = (_, settings) => new ServiceBusClient(settings.ConnectionString);
        AdminClientFactory = (_, settings) => new ServiceBusAdministrationClient(settings.ConnectionString);

        SenderFactory = (path, client) => client.CreateSender(path);

        ProcessorOptionsFactory = (p) => new ServiceBusProcessorOptions();
        ProcessorFactory = (p, options, client) =>
        {
            return p.SubscriptionName != null
                ? client.CreateProcessor(p.Path, p.SubscriptionName, options)
                : client.CreateProcessor(p.Path, options);
        };

        SessionProcessorOptionsFactory = (p) => new ServiceBusSessionProcessorOptions();
        SessionProcessorFactory = (p, options, client) =>
        {
            return p.SubscriptionName != null
                ? client.CreateSessionProcessor(p.Path, p.SubscriptionName, options)
                : client.CreateSessionProcessor(p.Path, options);
        };

        TopologyProvisioning = new ServiceBusTopologySettings();
    }

    public ServiceBusMessageBusSettings(string serviceBusConnectionString)
        : this()
    {
        ConnectionString = serviceBusConnectionString;
    }

    /// <summary>
    /// Configures the default subscription name when consuming form Azure ServiceBus topic.
    /// </summary>
    /// <param name="subscriptionName"></param>
    /// <returns></returns>
    public ServiceBusMessageBusSettings SubscriptionName(string subscriptionName)
    {
        if (subscriptionName is null) throw new ArgumentNullException(nameof(subscriptionName));

        this.SetSubscriptionName(subscriptionName);
        return this;
    }

    /// <summary>
    /// Allows to set additional properties to the native <see cref="ServiceBusMessage"/> when producing the any message.
    /// </summary>
    /// <param name="modifier"></param>
    /// <param name="executePrevious">Should the previously set modifier be executed as well?</param>
    /// <returns></returns>
    public ServiceBusMessageBusSettings WithModifier(AsbMessageModifier<object> modifier, bool executePrevious = true)
    {
        if (modifier is null) throw new ArgumentNullException(nameof(modifier));

        var previousModifier = executePrevious ? GetOrDefault(AsbProperties.MessageModifier) : null;
        AsbProperties.MessageModifier.Set(this, previousModifier == null
            ? modifier
            : (message, transportMessage) =>
            {
                previousModifier(message, transportMessage);
                modifier(message, transportMessage);
            });
        return this;
    }

    /// <summary>
    /// Allows to set additional properties to the native <see cref="ServiceBusMessage"/> when producing the any message.
    /// </summary>
    /// <param name="modifier"></param>
    /// <param name="executePrevious">Should the previously set modifier be executed as well?</param>
    /// <returns></returns>
    public ServiceBusMessageBusSettings WithModifier<T>(AsbMessageModifier<T> modifier, bool executePrevious = true)
        => WithModifier((message, transportMessage) =>
            {
                if (message is T typedMessage)
                {
                    modifier(typedMessage, transportMessage);
                }
            },
            executePrevious);
}