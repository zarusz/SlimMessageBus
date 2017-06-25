using System;
using System.IO;
using Microsoft.ServiceBus.Messaging;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureEventHub
{
    public class EventHubMessageBusSettings
    {
        /// <summary>
        /// The Azure Event Hub connection string.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// The Azure Storage connection string. This will store all the group consumer offsets.
        /// </summary>
        public string StorageConnectionString { get; set; }

        /// <summary>
        /// Factory for <see cref="EventHubClient"/>. Called whenever a new instance needs to be created.
        /// </summary>
        public Func<string, EventHubClient> EventHubClientFactory { get; set; }

        /// <summary>
        /// Factory for <see cref="EventProcessorHost"/>. Called whenever a new instance needs to be created.
        /// The func arguments are as follows: EventHubPath, Group.
        /// </summary>
        public Func<ConsumerSettings, EventProcessorHost> EventProcessorHostFactory { get; set; }

        /// <summary>
        /// Factory for <see cref="EventProcessorOptions"/>.
        /// The func arguments are as follows: EventHubPath, Group.
        /// </summary>
        public Func<ConsumerSettings, EventProcessorOptions> EventProcessorOptionsFactory { get; set; }

        /// <summary>
        /// Provides the HostName for this options. By default a GUID string provider is used.
        /// </summary>
        public Func<string> HostNameProvider { get; set; }

        public EventHubMessageBusSettings(string connectionString, string storageConnectionString)
        {
            ConnectionString = connectionString;
            StorageConnectionString = storageConnectionString;
            EventHubClientFactory = (path) => EventHubClient.CreateFromConnectionString(ConnectionString, path);
            EventProcessorHostFactory = (consumerSettings) => new EventProcessorHost(HostNameProvider(), consumerSettings.Topic, consumerSettings.Group, ConnectionString, StorageConnectionString);
            EventProcessorOptionsFactory = (consumerSettings) => EventProcessorOptions.DefaultOptions;
            HostNameProvider = () => Guid.NewGuid().ToString("N");
        }
    }
}