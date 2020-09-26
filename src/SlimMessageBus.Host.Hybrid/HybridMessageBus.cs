using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.Hybrid
{
    public class HybridMessageBus : IMessageBus
    {
        private readonly ILogger _logger;
        
        public ILoggerFactory LoggerFactory { get; }

        public MessageBusSettings Settings { get; }
        public HybridMessageBusSettings ProviderSettings { get; }

        private readonly IDictionary<Type, string> _routeByMessageType;
        private readonly IDictionary<string, MessageBusBase> _busByName;
        

        public HybridMessageBus(MessageBusSettings settings, HybridMessageBusSettings providerSettings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            ProviderSettings = providerSettings ?? throw new ArgumentNullException(nameof(providerSettings));

            // Use the configured logger factory, if not provided try to resolve from DI, if also not available supress logging using the NullLoggerFactory
            LoggerFactory = settings.LoggerFactory
                ?? (ILoggerFactory)settings.DependencyResolver?.Resolve(typeof(ILoggerFactory))
                ?? NullLoggerFactory.Instance;

            _logger = LoggerFactory.CreateLogger<HybridMessageBus>();

            _routeByMessageType = new Dictionary<Type, string>();

            _busByName = new Dictionary<string, MessageBusBase>(providerSettings.Count);
            foreach (var name in providerSettings.Keys)
            {
                var builderFunc = providerSettings[name];

                var bus = BuildBus(builderFunc);

                _busByName.Add(name, bus);

                BuildAutoRouting(name, bus);
            }

            // ToDo: defer start of busses until here
        }

        protected virtual MessageBusBase BuildBus(Action<MessageBusBuilder> builderFunc)
        {
            var builder = MessageBusBuilder.Create();
            builder.MergeFrom(Settings);
            builderFunc(builder);

            var bus = builder.Build();

            return (MessageBusBase)bus;
        }

        private void BuildAutoRouting(string name, MessageBusBase bus)
        {
            foreach (var producer in bus.Settings.Producers)
            {
                _routeByMessageType.Add(producer.MessageType, name);
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    foreach (var name in _busByName.Keys)
                    {
                        var bus = _busByName[name];

                        bus.DisposeSilently(() => $"Error dispsing name bus: {name}", _logger);
                    }
                    _busByName.Clear();
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        protected virtual IMessageBus Route(object message, string name)
        {
            var messageType = message.GetType();

            // Until we reached the object in class hierarchy
            while (messageType != null && messageType != typeof(object))
            {
                if (_routeByMessageType.TryGetValue(messageType, out var busName))
                {
                    _logger.LogDebug("Resolved bus {0} for message type: {1} and name {2}", busName, messageType, name);

                    return _busByName[busName];
                }

                // Check base type
                messageType = messageType.BaseType;
            }

            throw new ConfigurationMessageBusException($"Could not find route for message type: {message.GetType()} and name: {name}");
        }

        #region Implementation of IRequestResponseBus

        public Task<TResponseMessage> Send<TResponseMessage>(IRequestMessage<TResponseMessage> request, CancellationToken cancellationToken)
        {
            var bus = Route(request, null);
            return bus.Send(request, cancellationToken);
        }

        public Task<TResponseMessage> Send<TResponseMessage>(IRequestMessage<TResponseMessage> request, string name = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var bus = Route(request, name);
            return bus.Send(request, name, cancellationToken);
        }

        public Task<TResponseMessage> Send<TResponseMessage>(IRequestMessage<TResponseMessage> request, TimeSpan timeout, string name = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var bus = Route(request, name);
            return bus.Send(request, timeout, name, cancellationToken);
        }

        #endregion

        #region Implementation of IPublishBus

        public Task Publish<TMessage>(TMessage message, string name = null)
        {
            var bus = Route(message, name);
            return bus.Publish(message, name);
        }

        #endregion
    }
}
