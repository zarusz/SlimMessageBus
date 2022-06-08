namespace SlimMessageBus.Host.Memory
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using SlimMessageBus.Host.Config;
    using SlimMessageBus.Host.Serialization;

    /// <summary>
    /// In-memory message bus <see cref="IMessageBus"/> implementation to use for in process message passing.
    /// </summary>
    public class MemoryMessageBus : MessageBusBase
    {
        private readonly ILogger _logger;
        private IDictionary<string, List<MessageHandler>> _handlersByPath;
        private readonly IMessageSerializer _serializer;

        private MemoryMessageBusSettings ProviderSettings { get; }

        public override IMessageSerializer Serializer => _serializer;

        public MemoryMessageBus(MessageBusSettings settings, MemoryMessageBusSettings providerSettings) : base(settings)
        {
            _logger = LoggerFactory.CreateLogger<MemoryMessageBus>();
            ProviderSettings = providerSettings ?? throw new ArgumentNullException(nameof(providerSettings));

            OnBuildProvider();

            _serializer = ProviderSettings.EnableMessageSerialization
                ? Settings.Serializer
                : new NullMessageSerializer();
        }

        #region Overrides of MessageBusBase

        protected override void AssertSerializerSettings()
        {
            if (ProviderSettings.EnableMessageSerialization)
            {
                base.AssertSerializerSettings();
            }
        }

        protected override void Build()
        {
            base.Build();

            _handlersByPath = Settings.Consumers
                .GroupBy(x => x.Path)
                .ToDictionary(x => x.Key, x => x.Select(consumerSettings => new MessageHandler(consumerSettings, this)).ToList());
        }

        public override async Task ProduceToTransport(object message, string path, byte[] messagePayload, IDictionary<string, object> messageHeaders, CancellationToken cancellationToken)
        {
            var messageType = message.GetType();
            if (!_handlersByPath.TryGetValue(path, out var consumers) || consumers.Count == 0)
            {
                _logger.LogDebug("No consumers interested in message type {MessageType} on path {Path}", messageType, path);
                return;
            }

            // ToDo: Extension: In case of IMessageBus.Publish do not wait for the consumer method see https://github.com/zarusz/SlimMessageBus/issues/37

            foreach (var consumer in consumers)
            {
                _logger.LogDebug("Executing consumer {ConsumerType} on {Message}...", consumer.ConsumerSettings.ConsumerType, message);
                await OnMessageProduced(messageType, message, path, messagePayload, messageHeaders, consumer, cancellationToken);
                _logger.LogTrace("Executed consumer {ConsumerType}", consumer.ConsumerSettings.ConsumerType);
            }
        }

        private async Task OnMessageProduced(Type messageType, object message, string path, byte[] messagePayload, IDictionary<string, object> messageHeaders, MessageHandler consumer, CancellationToken cancellationToken)
        {
            object response = null;
            string requestId = null;

            Exception responseException;
            try
            {
                // will pass a deep copy of the message (if serialization enabled) or the original message if serialization not enabled
                var messageForConsumer = Serializer.Deserialize(messageType, messagePayload) ?? message;

                (response, responseException, requestId) = await consumer.DoHandle(message: messageForConsumer, messageHeaders: messageHeaders, nativeMessage: null).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.LogDebug(e, "Error occured while executing {ConsumerType} on {Message}", consumer.ConsumerSettings.ConsumerType, message);

                OnMessageFailed(message, consumer, e);

                if (consumer.ConsumerSettings.ConsumerMode == ConsumerMode.RequestResponse)
                {
                    // When in request response mode then pass the error back to the sender
                    responseException = e;
                }
                else
                {
                    // Otherwise rethrow the error
                    throw;
                }
            }

            if (consumer.ConsumerSettings.ConsumerMode == ConsumerMode.RequestResponse)
            {
                // will be null when serialization is not enabled
                var responsePayload = Serializer.Serialize(consumer.ConsumerSettings.ResponseType, response);

                await OnResponseArrived(responsePayload, path, requestId, responseException, response).ConfigureAwait(false);
            }
        }

        private void OnMessageFailed(object message, MessageHandler consumer, Exception e)
        {
            try
            {
                // Invoke fault handler.
                consumer.ConsumerSettings.OnMessageFault?.Invoke(this, consumer.ConsumerSettings, message, e, null);
                Settings.OnMessageFault?.Invoke(this, consumer.ConsumerSettings, message, e, null);
            }
            catch (Exception hookEx)
            {
                HookFailed(_logger, hookEx, nameof(consumer.ConsumerSettings.OnMessageFault));
            }
        }

        #endregion

        public override bool IsMessageScopeEnabled(ConsumerSettings consumerSettings)
            => (consumerSettings ?? throw new ArgumentNullException(nameof(consumerSettings))).IsMessageScopeEnabled ?? Settings.IsMessageScopeEnabled ?? false; // by default Memory Bus has scoped message disabled
    }
}
