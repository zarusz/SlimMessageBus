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
        /// Default configuration action called when any queue needs to be created.
        /// </summary>
        public Action<CreateQueueOptions> QueueOptions { get; set; }
        /// <summary>
        /// Default configuration action called when any topic needs to be created.
        /// </summary>
        public Action<CreateTopicOptions> TopicOptions { get; set; }
        /// <summary>
        /// Default configuration action called when any subscription needs to be created.
        /// </summary>
        public Action<CreateSubscriptionOptions> SubscriptionOptions { get; set; }
    }
}