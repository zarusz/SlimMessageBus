using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using SlimMessageBus.Host.DependencyResolver;
using SlimMessageBus.Host.Serialization;

namespace SlimMessageBus.Host.Config
{
    public class MessageBusSettings : IBusEvents
    {
        public ILoggerFactory LoggerFactory { get; set; }
        public IList<ProducerSettings> Producers { get; }
        public IList<ConsumerSettings> Consumers { get; }
        public RequestResponseSettings RequestResponse { get; set; }
        public IMessageSerializer Serializer { get; set; }
        /// <summary>
        /// Dedicated <see cref="IMessageSerializer"/> capable of serializing <see cref="MessageWithHeaders"/>.
        /// By default uses <see cref="MessageWithHeadersSerializer"/>.
        /// </summary>
        public IMessageSerializer MessageWithHeadersSerializer { get; set; }
        public IDependencyResolver DependencyResolver { get; set; }

        #region Implementation of IConsumerEvents
        ///
        /// <inheritdoc/>
        ///
        public Action<IMessageBus, AbstractConsumerSettings, object, string> OnMessageArrived { get; set; }
        ///
        /// <inheritdoc/>
        ///
        public Action<IMessageBus, AbstractConsumerSettings, object> OnMessageExpired { get; set; }
        ///
        /// <inheritdoc/>
        ///
        public Action<IMessageBus, AbstractConsumerSettings, object, Exception> OnMessageFault { get; set; }
        #endregion

        #region Implementation of IProducerEvents

        public Action<IMessageBus, ProducerSettings, object, string> OnMessageProduced { get; set; }

        #endregion

        public MessageBusSettings()
        {
            Producers = new List<ProducerSettings>();
            Consumers = new List<ConsumerSettings>();
            MessageWithHeadersSerializer = new MessageWithHeadersSerializer();
        }

        public virtual void MergeFrom(MessageBusSettings settings)
        {
            if (settings is null) throw new ArgumentNullException(nameof(settings));

            if (LoggerFactory == null && settings.LoggerFactory != null)
            {
                LoggerFactory = settings.LoggerFactory;
            }

            if (settings.Producers.Count > 0)
            {
                foreach (var p in settings.Producers)
                {
                    Producers.Add(p);
                }
            }

            if (settings.Consumers.Count > 0)
            {
                foreach (var c in settings.Consumers)
                {
                    Consumers.Add(c);
                }
            }

            if (Serializer == null && settings.Serializer != null)
            {
                Serializer = settings.Serializer;
            }

            if (RequestResponse == null && settings.RequestResponse != null)
            {
                RequestResponse = settings.RequestResponse;
            }

            if (Serializer == null && settings.Serializer != null)
            {
                Serializer = settings.Serializer;
            }

            if (DependencyResolver == null && settings.DependencyResolver != null)
            {
                DependencyResolver = settings.DependencyResolver;
            }

            if (OnMessageArrived == null && settings.OnMessageArrived != null)
            {
                OnMessageArrived = settings.OnMessageArrived;
            }

            if (OnMessageExpired == null && settings.OnMessageExpired != null)
            {
                OnMessageExpired = settings.OnMessageExpired;
            }

            if (OnMessageFault == null && settings.OnMessageFault != null)
            {
                OnMessageFault = settings.OnMessageFault;
            }

            if (OnMessageProduced == null && settings.OnMessageProduced != null)
            {
                OnMessageProduced = settings.OnMessageProduced;
            }
        }
    }
}