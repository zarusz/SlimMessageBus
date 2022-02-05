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