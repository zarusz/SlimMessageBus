namespace SlimMessageBus.Host.AzureServiceBus;

using Azure.Messaging.ServiceBus.Administration;
using System;

public class ServiceBusTopologyProvisioningSettings
{
    /// <summary>
    /// Indicates wheather topology provisioning is enabled. Default is true.
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
}