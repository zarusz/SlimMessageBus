using System;
using Microsoft.ServiceBus.Messaging;

namespace SlimMessageBus.Host.AzureEventHub
{
    public class EventHubMessageBusSettings
    {
        /// <summary>
        /// The Kafka broker nodes. This corresponds to "bootstrap.servers" Kafka setting.
        /// </summary>
        public string ConnectionString { get; set; }

        public Func<string, EventHubClient> EventHubClientFactory { get; set; }

        public EventHubMessageBusSettings(string connectionString)
        {
            ConnectionString = connectionString;
            EventHubClientFactory = (path) => EventHubClient.CreateFromConnectionString(ConnectionString, path);
        }
    }
}