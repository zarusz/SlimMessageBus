using System;
using Microsoft.Azure.ServiceBus;

namespace SlimMessageBus.Host.AzureServiceBus
{
    public class ServiceBusMessageBusSettings
    {
        public string ServiceBusConnectionString { get; set; }

        public Func<TopicClientFactoryParams, TopicClient> TopicClientFactory { get; set; }
        public Func<SubscriptionFactoryParams, SubscriptionClient> SubscriptionClientFactory { get; set; }

        public ServiceBusMessageBusSettings()
        {
            TopicClientFactory = x => new TopicClient(ServiceBusConnectionString, x.Path);
            SubscriptionClientFactory = x => new SubscriptionClient(ServiceBusConnectionString, x.Path, x.SubscriptionName);
        }

        public ServiceBusMessageBusSettings(string serviceBusConnectionString)
            : this()
        {
            ServiceBusConnectionString = serviceBusConnectionString;
        }
    }

    public class TopicClientFactoryParams
    {
        public string Path { get; set; }

        public TopicClientFactoryParams(string path)
        {
            Path = path;
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