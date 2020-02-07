using System;
using Microsoft.Azure.ServiceBus;

namespace SlimMessageBus.Host.AzureServiceBus
{
    public class ServiceBusMessageBusSettings
    {
        public string ServiceBusConnectionString { get; set; }

        public Func<string, ITopicClient> TopicClientFactory { get; set; }
        public Func<string, IQueueClient> QueueClientFactory { get; set; }
        public Func<SubscriptionFactoryParams, SubscriptionClient> SubscriptionClientFactory { get; set; }

        public ServiceBusMessageBusSettings()
        {
            TopicClientFactory = x => new TopicClient(ServiceBusConnectionString, x);
            QueueClientFactory = x => new QueueClient(ServiceBusConnectionString, x);
            SubscriptionClientFactory = x => new SubscriptionClient(ServiceBusConnectionString, x.Path, x.SubscriptionName);
        }

        public ServiceBusMessageBusSettings(string serviceBusConnectionString)
            : this()
        {
            ServiceBusConnectionString = serviceBusConnectionString;
        }
    }
}