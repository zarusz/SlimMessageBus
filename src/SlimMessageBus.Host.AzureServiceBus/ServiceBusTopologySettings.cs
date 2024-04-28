namespace SlimMessageBus.Host.AzureServiceBus;

public class ServiceBusTopologySettings
{
    /// <summary>
    /// Indicates whether topology provisioning is enabled. Default is true.
    /// </summary>
    public bool Enabled { get; set; } = true;
    /// <summary>
    /// A filter that allows (or not) for declared producers to provision needed queues. True by default.
    /// </summary>
    public bool CanProducerCreateQueue { get; set; } = true;
    /// <summary>
    /// A filter that allows (or not) for declared producers to provision needed topic. True by default.
    /// </summary>
    public bool CanProducerCreateTopic { get; set; } = true;
    /// <summary>
    /// A filter that allows (or not) for declared consumers to provision needed queues. True by default.
    /// </summary>
    public bool CanConsumerCreateQueue { get; set; } = true;
    /// <summary>
    /// A filter that allows (or not) for declared consumers to provision needed topics. True by default.
    /// </summary>
    public bool CanConsumerCreateTopic { get; set; } = true;
    /// <summary>
    /// A filter that allows (or not) for declared consumers to provision needed subscription. True by default.
    /// </summary>
    public bool CanConsumerCreateSubscription { get; set; } = true;
    /// <summary>
    /// A filter that allows (or not) for declared consumers to provision needed filter. True by default.
    /// </summary>
    public bool CanConsumerCreateSubscriptionFilter { get; set; } = true;
    /// <summary>
    /// A filter that allows (or not) for declared consumers to replace already defined filters. False by default.
    /// </summary>
    public bool CanConsumerReplaceSubscriptionFilters { get; set; } = false;
    /// <summary>
    /// A filter that allows (or not) for declared consumers to validate that filters match expectations. True by default.
    /// </summary>
    public bool CanConsumerValidateSubscriptionFilters { get; set; } = true;
    /// <summary>
    /// Default configuration to be applied when a queue needs to be created (<see cref="CreateQueueOptions"/>).
    /// </summary>
    public Action<CreateQueueOptions> CreateQueueOptions { get; set; }
    /// <summary>
    /// Default configuration to be applied when a topic needs to be created (<see cref="CreateTopicOptions"/>).
    /// </summary>
    public Action<CreateTopicOptions> CreateTopicOptions { get; set; }
    /// <summary>
    /// Default configuration to be applied when a subscription needs to be created (<see cref="CreateSubscriptionOptions"/>).
    /// </summary>
    public Action<CreateSubscriptionOptions> CreateSubscriptionOptions { get; set; }
    /// <summary>
    /// Default configuration to be applied when a rule needs to be created (<see cref="CreateSubscriptionFilterOptions"/>).
    /// </summary>
    public Action<CreateRuleOptions> CreateSubscriptionFilterOptions { get; set; }
    /// <summary>
    /// Interceptor that allows to intercept the topology provisioning process.
    /// </summary>
    public ServiceBusTopologyInterceptor OnProvisionTopology { get; set; } = (client, provision) => provision();
}

/// <summary>
/// Interceptor that allows to intercept the topology provisioning process and to apply custom logic before and after the provisioning process. 
/// </summary>
/// <param name="client">Service Bus admin client</param>
/// <param name="provision">Delegate allowing to perform topology provisioning</param>
/// <returns></returns>
public delegate Task ServiceBusTopologyInterceptor(ServiceBusAdministrationClient client, Func<Task> provision);
