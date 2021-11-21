namespace SlimMessageBus.Host.AzureEventHub
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.EventHubs;
    using Microsoft.Extensions.Logging;
    using SlimMessageBus.Host.Collections;
    using SlimMessageBus.Host.Config;

    /// <summary>
    /// MessageBus implementation for Azure Event Hub.
    /// </summary>
    public class EventHubMessageBus : MessageBusBase
    {
        private readonly ILogger logger;

        public EventHubMessageBusSettings ProviderSettings { get; }

        private SafeDictionaryWrapper<string, EventHubClient> producerByPath;
        private readonly List<EhGroupConsumer> groupConsumers = new List<EhGroupConsumer>();

        public EventHubMessageBus(MessageBusSettings settings, EventHubMessageBusSettings eventHubSettings)
            : base(settings)
        {
            logger = LoggerFactory.CreateLogger<EventHubMessageBus>();
            ProviderSettings = eventHubSettings;

            OnBuildProvider();
        }

        #region Overrides of MessageBusBase

        protected override void Build()
        {
            base.Build();

            producerByPath = new SafeDictionaryWrapper<string, EventHubClient>(path =>
            {
                logger.LogDebug("Creating EventHubClient for path {Path}", path);
                return ProviderSettings.EventHubClientFactory(path);
            });

            logger.LogInformation("Creating consumers");
            foreach (var consumerSettings in Settings.Consumers)
            {
                logger.LogInformation("Creating consumer for Path: {Path}, Group: {Group}, MessageType: {MessageType}", consumerSettings.Path, consumerSettings.GetGroup(), consumerSettings.MessageType);
                groupConsumers.Add(new EhGroupConsumer(this, consumerSettings));
            }

            if (Settings.RequestResponse != null)
            {
                logger.LogInformation("Creating response consumer for Path: {Path}, Group: {Group}", Settings.RequestResponse.Path, Settings.RequestResponse.GetGroup());
                groupConsumers.Add(new EhGroupConsumer(this, Settings.RequestResponse));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (groupConsumers != null)
                {
                    groupConsumers.ForEach(c => c.DisposeSilently("Consumer", logger));
                    groupConsumers.Clear();
                }

                if (producerByPath != null)
                {
                    producerByPath.Clear(producer =>
                    {
                        logger.LogDebug("Closing EventHubClient for Path {Path}", producer.EventHubName);
                        try
                        {
                            producer.Close();
                        }
                        catch (Exception e)
                        {
                            logger.LogError(e, "Error while closing EventHubClient for Path {Path}", producer.EventHubName);
                        }
                    });
                }
            }
            base.Dispose(disposing);
        }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="messageType"></param>
        /// <param name="payload"></param>
        /// <param name="message"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public override async Task ProduceToTransport(Type messageType, object message, string path, byte[] messagePayload, IDictionary<string, object> messageHeaders = null)
        {
            if (messageType is null) throw new ArgumentNullException(nameof(messageType));
            if (messagePayload is null) throw new ArgumentNullException(nameof(messagePayload));

            AssertActive();

            logger.LogDebug("Producing message {Message} of Type {MessageType} on Path {Path} with Size {MessageSize}", message, messageType.Name, path, messagePayload.Length);

            using var ev = new EventData(messagePayload);

            if (messageHeaders != null)
            {
                foreach (var header in messageHeaders)
                {
                    ev.Properties.Add(header.Key, header.Value);
                }
            }

            var partitionKey = GetPartitionKey(messageType, message);

            var producer = producerByPath.GetOrAdd(path);

            var sendTask = partitionKey != null 
                ? producer.SendAsync(ev, partitionKey) 
                : producer.SendAsync(ev);

            await sendTask.ConfigureAwait(false);

            logger.LogDebug("Delivered message {Message} of Type {MessageType} on Path {Path} with PartitionKey {PartitionKey}", message, messageType.Name, path, partitionKey);
        }

        private string GetPartitionKey(Type messageType, object message)
        {
            do
            {
                if (ProducerSettingsByMessageType.TryGetValue(messageType, out var producerSettings))
                {
                    try
                    {
                        var keyProvider = producerSettings.GetKeyProvider();
                        var partitionKey = keyProvider?.Invoke(message);
                        return partitionKey;
                    }
                    catch (Exception e)
                    {
                        logger.LogWarning(e, "The configured message KeyProvider failed for message type {MessageType} and message {Message}", messageType, message);
                    }
                }
                messageType = messageType.BaseType;
            } while (messageType != typeof(object));

            return null;
        }
    }
}
