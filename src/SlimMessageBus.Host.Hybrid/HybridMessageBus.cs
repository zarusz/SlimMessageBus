using System;
using System.Threading;
using System.Threading.Tasks;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.Hybrid
{
    public class HybridMessageBus : IMessageBus
    {
        public MessageBusSettings Settings { get; }
        public HybridMessageBusSettings HybridSettings { get; }

        public HybridMessageBus(MessageBusSettings settings, HybridMessageBusSettings hybridSettings)
        {
            Settings = settings;
            HybridSettings = hybridSettings;


        }
        
        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Implementation of IRequestResponseBus

        public Task<TResponseMessage> Send<TResponseMessage>(IRequestMessage<TResponseMessage> request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<TResponseMessage> Send<TResponseMessage>(IRequestMessage<TResponseMessage> request, string name = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public Task<TResponseMessage> Send<TResponseMessage>(IRequestMessage<TResponseMessage> request, TimeSpan timeout, string name = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Implementation of IPublishBus

        public Task Publish<TMessage>(TMessage message, string name = null)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
