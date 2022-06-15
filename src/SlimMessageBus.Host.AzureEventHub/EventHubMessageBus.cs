namespace SlimMessageBus.Host.AzureEventHub
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.EventHubs;
    using Azure.Messaging.EventHubs.Producer;
    using Azure.Storage.Blobs;
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

        private BlobContainerClient blobContainerClient;
        private SafeDictionaryWrapper<string, EventHubProducerClient> producerByPath;
        private List<EhGroupConsumer> groupConsumers;

        protected internal BlobContainerClient BlobContainerClient => blobContainerClient;

        public EventHubMessageBus(MessageBusSettings settings, EventHubMessageBusSettings eventHubSettings)
            : base(settings)
        {
            logger = LoggerFactory.CreateLogger<EventHubMessageBus>();
            ProviderSettings = eventHubSettings;

            OnBuildProvider();
        }

        protected override void AssertSettings()
        {
            base.AssertSettings();

            if (IsAnyConsumerDeclared)
            {
                Assert.IsNotNull(ProviderSettings.StorageConnectionString,
                    () => new ConfigurationMessageBusException($"The {nameof(EventHubMessageBusSettings)}.{nameof(EventHubMessageBusSettings.StorageConnectionString)} is not set"));

                Assert.IsNotNull(ProviderSettings.LeaseContainerName,
                    () => new ConfigurationMessageBusException($"The {nameof(EventHubMessageBusSettings)}.{nameof(EventHubMessageBusSettings.LeaseContainerName)} is not set"));
            }
        }

        private bool IsAnyConsumerDeclared => Settings.Consumers.Count > 0 || Settings.RequestResponse != null;

        #region Overrides of MessageBusBase

        protected override void Build()
        {
            base.Build();

            // Initialize storage client only when there are consumers declared
            blobContainerClient = IsAnyConsumerDeclared
                ? ProviderSettings.BlobContanerClientFactory()
                : null;

            producerByPath = new SafeDictionaryWrapper<string, EventHubProducerClient>(path =>
            {
                logger.LogDebug("Creating EventHubClient for path {Path}", path);
                return ProviderSettings.EventHubProducerClientFactory(path);
            });

            groupConsumers = new List<EhGroupConsumer>();

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

        protected override async ValueTask DisposeAsyncCore()
        {
            await base.DisposeAsyncCore();

            if (groupConsumers != null)
            {
                foreach (var groupConsumer in groupConsumers)
                {
                    await groupConsumer.DisposeSilently("Consumer", logger);
                }
                groupConsumers.Clear();
                groupConsumers = null;
            }

            if (producerByPath != null)
            {
                var producers = producerByPath.ClearAndSnapshot();
                foreach (var producer in producers)
                {
                    logger.LogDebug("Closing EventHubProducerClient for Path {Path}", producer.EventHubName);
                    await ((IAsyncDisposable)producer).DisposeSilently();
                }
                producerByPath = null;
            }
        }

        protected override async Task OnStart()
        {
            await base.OnStart();

            if (blobContainerClient != null)
            {
                // Create blob storage container if not exists
                try
                {
                    await blobContainerClient.CreateIfNotExistsAsync();
                }
                catch (Exception e)
                {
                    logger.LogWarning(e, "Attempt to create blob container {BlobContainer} failed - the blob container is needed to store the consumer group offsets", blobContainerClient.Name);
                }
            }

            await Task.WhenAll(groupConsumers.Select(x => x.Start()));
        }

        protected override async Task OnStop()
        {
            await base.OnStop();
            await Task.WhenAll(groupConsumers.Select(x => x.Stop()));
        }

        public override async Task ProduceToTransport(object message, string path, byte[] messagePayload, IDictionary<string, object> messageHeaders, CancellationToken cancellationToken)
        {
            if (messagePayload is null) throw new ArgumentNullException(nameof(messagePayload));

            AssertActive();

            var messageType = message.GetType();

            logger.LogDebug("Producing message {Message} of Type {MessageType} on Path {Path} with Size {MessageSize}", message, messageType.Name, path, messagePayload.Length);

            var ev = new EventData(messagePayload);

            if (messageHeaders != null)
            {
                foreach (var header in messageHeaders)
                {
                    ev.Properties.Add(header.Key, header.Value);
                }
            }

            var partitionKey = GetPartitionKey(messageType, message);

            var producer = producerByPath.GetOrAdd(path);

            // ToDo: Introduce some micro batching of events (store them between invocations and send when time span elapsed)
            using EventDataBatch eventBatch = await producer.CreateBatchAsync(new CreateBatchOptions
            {
                // When null the partition will be automatically assigned
                PartitionKey = partitionKey
            }, cancellationToken);
            if (!eventBatch.TryAdd(ev))
            {
                throw new PublishMessageBusException($"Could not add message {message} of Type {messageType.Name} on Path {path} to the send batch");
            }

            await producer.SendAsync(eventBatch, cancellationToken).ConfigureAwait(false);

            logger.LogDebug("Delivered message {Message} of Type {MessageType} on Path {Path} with PartitionKey {PartitionKey}", message, messageType.Name, path, partitionKey);
        }

        #endregion

        private string GetPartitionKey(Type messageType, object message)
        {
            var producerSettings = GetProducerSettings(messageType);
            try
            {
                var keyProvider = producerSettings?.GetKeyProvider();
                var partitionKey = keyProvider?.Invoke(message);
                return partitionKey;
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "The configured message KeyProvider failed for message type {MessageType} and message {Message}", messageType, message);
            }
            return null;
        }
    }
}
