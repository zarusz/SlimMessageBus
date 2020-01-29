using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Common.Logging;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.Memory
{
    /// <summary>
    /// In memory message bus <see cref="IMessageBus"/> implementation to use for in process message passing.
    /// </summary>
    public class MemoryMessageBus : MessageBusBase
    {
        private static readonly ILog Log = LogManager.GetLogger<MemoryMessageBus>();

        private MemoryMessageBusSettings ProviderSettings { get; }

        private IDictionary<string, List<ConsumerSettings>> _consumersByTopic;

        public MemoryMessageBus(MessageBusSettings settings, MemoryMessageBusSettings providerSettings)
            : base(settings)
        {
            ProviderSettings = providerSettings ?? throw new ArgumentNullException(nameof(providerSettings));
            OnBuildProvider();
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

            _consumersByTopic = Settings.Consumers
                .GroupBy(x => x.Topic)
                .ToDictionary(x => x.Key, x => x.ToList());
        }

        public override Task ProduceToTransport(Type messageType, object message, string name, byte[] payload)
        {
            if (!_consumersByTopic.TryGetValue(name, out var consumers))
            {
                Log.DebugFormat(CultureInfo.InvariantCulture, "No consumers interested in event {0} on topic {1}", messageType, name);
                return Task.CompletedTask;
            }

            var tasks = new LinkedList<Task>();
            foreach (var consumer in consumers)
            {
                // obtain the consumer from DI
                Log.DebugFormat(CultureInfo.InvariantCulture, "Resolving consumer type {0}", consumer.ConsumerType);
                var consumerInstance = Settings.DependencyResolver.Resolve(consumer.ConsumerType);
                if (consumerInstance == null)
                {
                    Log.WarnFormat(CultureInfo.InvariantCulture, "The dependency resolver did not yield any instance of {0}", consumer.ConsumerType);
                    continue;
                }

                if (consumer.IsRequestMessage)
                {
                    Log.Warn("The in memory provider only supports pub-sub communication for now");
                    continue;
                }

                var messageForConsumer = ProviderSettings.EnableMessageSerialization
                    ? DeserializeMessage(messageType, payload) // will pass a deep copy of the message
                    : message; // prevent deep copy of the message

                Log.DebugFormat(CultureInfo.InvariantCulture, "Invoking consumer {0}", consumerInstance.GetType());
                var task = consumer.ConsumerMethod(consumerInstance, message, consumer.Topic);

                tasks.AddLast(task);
            }

            Log.DebugFormat(CultureInfo.InvariantCulture, "Waiting on {0} consumer tasks", tasks.Count);
            return Task.WhenAll(tasks);
        }

        public override byte[] SerializeMessage(Type messageType, object message)
        {
            if (ProviderSettings.EnableMessageSerialization)
            {
                return base.SerializeMessage(messageType, message);
            }
            // the serialized payload is not going to be used
            return null;
        }

        #endregion
    }
}
