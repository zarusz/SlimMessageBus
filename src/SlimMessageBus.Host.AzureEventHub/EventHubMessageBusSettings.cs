namespace SlimMessageBus.Host.AzureEventHub
{
    using System;
    using Microsoft.Azure.EventHubs;
    using Microsoft.Azure.EventHubs.Processor;

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
        public Func<TopicGroup, EventProcessorHost> EventProcessorHostFactory { get; set; }

        /// <summary>
        /// Factory for <see cref="EventProcessorOptions"/>.
        /// The func arguments are as follows: EventHubPath, Group.
        /// </summary>
        public Func<TopicGroup, EventProcessorOptions> EventProcessorOptionsFactory { get; set; }

        /// <summary>
        /// The storage container name for leases.
        /// </summary>
        public string LeaseContainerName { get; set; }

        public EventHubMessageBusSettings(string connectionString, string storageConnectionString, string leaseContainerName)
        {
            ConnectionString = connectionString;
            StorageConnectionString = storageConnectionString;
            EventHubClientFactory = (path) =>
            {
                var connectionStringBuilder = new EventHubsConnectionStringBuilder(ConnectionString)
                {
                    EntityPath = path
                };
                return EventHubClient.CreateFromConnectionString(connectionStringBuilder.ToString());
            };
            EventProcessorHostFactory = (consumerSettings) => new EventProcessorHost(consumerSettings.Topic, consumerSettings.Group, ConnectionString, StorageConnectionString, LeaseContainerName);
            EventProcessorOptionsFactory = (consumerSettings) => EventProcessorOptions.DefaultOptions;
            LeaseContainerName = leaseContainerName;
        }
    }

    public class TopicGroup
    {
        public TopicGroup(string topic, string group)
        {
            Topic = topic;
            Group = group;
        }

        public string Topic { get; set; }
        public string Group { get; set; }
    }
}