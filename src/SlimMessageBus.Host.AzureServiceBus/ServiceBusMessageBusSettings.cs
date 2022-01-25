namespace SlimMessageBus.Host.AzureServiceBus
{
    using Azure.Messaging.ServiceBus;
    using System;

    public class ServiceBusMessageBusSettings
    {
        public string ServiceBusConnectionString { get; set; }
        public Func<ServiceBusClient> ClientFactory { get; set; }
        public Func<string, ServiceBusClient, ServiceBusSender> SenderFactory { get; set; }
        public Func<TopicSubscriptionParams, ServiceBusProcessorOptions, ServiceBusClient, ServiceBusProcessor> ProcessorFactory { get; set; }

        public ServiceBusMessageBusSettings()
        {
            ClientFactory = () => new ServiceBusClient(ServiceBusConnectionString);
            SenderFactory = (path, client) => client.CreateSender(path);
            ProcessorFactory = (p, options, client) => p.SubscriptionName != null
                ? client.CreateProcessor(p.Path, p.SubscriptionName, options)
                : client.CreateProcessor(p.Path, options);
        }

        public ServiceBusMessageBusSettings(string serviceBusConnectionString)
            : this()
        {
            ServiceBusConnectionString = serviceBusConnectionString;
        }
    }
}