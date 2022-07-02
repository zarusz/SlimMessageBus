namespace SlimMessageBus.Host.AzureServiceBus
{
    using Azure.Messaging.ServiceBus.Administration;
    using System;

    public class ServiceBusTopologyProvisioningSettings
    {
        /// <summary>
        /// Indicates wheather topology provisioning is enabled. Default is true.
        /// </summary>
        public bool Enabled { get; set; } = true;
        /// <summary>
        /// A filter that allows (or not) for declared producers to provision needed topology (topics or queues). True by default.
        /// </summary>
        public bool UseDeclaredProducers { get; set; } = true;
        /// <summary>
        /// A filter that allows (or not) for declared consumers to provision needed topology (topics, subscriptions or queues). True by default.
        /// </summary>
        public bool UserDeclaredConsumers{ get; set; } = true;
        /// <summary>
        /// Default configuration to be applied when a queue needs to be created.
        /// </summary>
        public Action<CreateQueueOptions> CreateQueueOptions { get; set; }
        /// <summary>
        /// Default configuration to be applied when a topic needs to be created.
        /// </summary>
        public Action<CreateTopicOptions> CreateTopicOptions { get; set; }
        /// <summary>
        /// Default configuration to be applied when a subscription needs to be created.
        /// </summary>
        public Action<CreateSubscriptionOptions> CreateSubscriptionOptions { get; set; }
    }
}