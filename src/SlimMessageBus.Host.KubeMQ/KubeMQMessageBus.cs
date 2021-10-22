using KubeMQ.SDK.csharp.Tools;
using KubeMQ.SDK.csharp.Events;

namespace SlimMessageBus.Host.KubeMQ
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using SlimMessageBus.Host.Config;
    using SlimMessageBus.Host.KubeMQ.Configs;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// <see cref="IMessageBus"/> implementation for KubeMQ.
    /// Note
    /// </summary>
    public class KubeMQMessageBus : MessageBusBase
    {
        private readonly ILogger _logger;

        public KubeMQMessageBusSettings ProviderSettings { get; }

        private readonly IDictionary<Type, Channel> _channels = new Dictionary<Type, Channel>();

        public KubeMQMessageBus(MessageBusSettings settings, KubeMQMessageBusSettings providerSettings)
            : base(settings)
        {
            _logger = LoggerFactory.CreateLogger<KubeMQMessageBus>();
            ProviderSettings = providerSettings ?? throw new ArgumentNullException(nameof(providerSettings));

            OnBuildProvider();

            // TODO: Auto start should be a setting
            Start();
        }

        protected override void Build()
        {
            base.Build();

            CreateProviders();
        }

        private void CreateProviders()
        {
            foreach (var producerSettings in Settings.Producers)
            {
                _logger.LogDebug($"keyProvider: {producerSettings.DefaultPath}. Type: {producerSettings.MessageType}");
                
                var channel = new Channel(new ChannelParameters
                {
                    ChannelName = producerSettings.DefaultPath,
                    ClientID = ProviderSettings.ClientID,
                    KubeMQAddress = ProviderSettings.ServerAddress
                });
                _channels.Add(producerSettings.MessageType, channel);
            }
        }

        private void Start()
        {
            _logger.LogInformation("Group consumers starting...");
            _logger.LogInformation("Group consumers started");
        }

        protected override void AssertSettings()
        {
            base.AssertSettings();

            foreach (var consumer in Settings.Consumers)
            {
                Assert.IsTrue(consumer.GetGroup() != null,
                    () => new ConfigurationMessageBusException($"Consumer ({consumer.MessageType}): group was not provided"));
            }

            if (Settings.RequestResponse != null)
            {
                Assert.IsTrue(Settings.RequestResponse.GetGroup() != null,
                    () => new ConfigurationMessageBusException("Request-response: group was not provided"));

                Assert.IsFalse(Settings.Consumers.Any(x => x.GetGroup() == Settings.RequestResponse.GetGroup() && x.Path == Settings.RequestResponse.Path),
                    () => new ConfigurationMessageBusException("Request-response: cannot use topic that is already being used by a consumer"));
            }
        }

        #region Overrides of BaseMessageBus

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                AssertActive();

                if (_channels.Count > 0)
                {
                    _channels.Clear();
                }
            }
            base.Dispose(disposing);
        }

        public override async Task ProduceToTransport([NotNull] Type messageType, object message, string name, [NotNull] byte[] messagePayload, MessageWithHeaders messageWithHeaders = null)
        {
            AssertActive();

            _logger.LogTrace("Producing message {message} of type {messageType}, on topic {topic}, payload size {messageSize}",
                message, messageType.Name, name, messagePayload.Length);

            try
            {
                var channel = _channels[messageType];
                var result = channel.SendEvent(new Event()
                {                  
                    Body = Converter.ToByteArray(message)
                });
                if (!result.Sent)
                {
                    _logger.LogWarning($"Could not send message:{result.Error}");
                }else 
                {
                    _logger.LogDebug($"Message delivered, event id:{result.EventID}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
        }

        #endregion
    }
}