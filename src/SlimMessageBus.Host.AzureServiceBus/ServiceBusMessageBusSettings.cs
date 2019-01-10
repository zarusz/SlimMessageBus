using System;
using Microsoft.Azure.ServiceBus;

namespace SlimMessageBus.Host.AzureServiceBus
{
    public class ServiceBusMessageBusSettings
    {
        public string ServiceBusConnectionString { get; set; }

        public Func<string, TopicClient> TopicClientFactory { get; set; }
        public Func<string, QueueClient> QueueClientFactory { get; set; }
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

    public class SubscriptionFactoryParams
    {
        public string Path { get; set; }
        public string SubscriptionName { get; set; }

        public SubscriptionFactoryParams(string path, string subscriptionName)
        {
            Path = path;
            SubscriptionName = subscriptionName;
        }
    }
}