namespace SlimMessageBus.Host
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using SlimMessageBus.Host.DependencyResolver;

    /// <summary>
    /// Proxy to the <see cref="IMessageBusBase"/> that introduces its own <see cref="IDependencyResolver"/> for dependency lookup.
    /// </summary>
    public class MessageBusProxy : IMessageBus
    {
        /// <summary>
        /// The target of this proxy (the singleton master bus).
        /// </summary>
        public IMessageBusProducer Target { get; }
        private readonly IDependencyResolver dependencyResolver;

        public MessageBusProxy(IMessageBusProducer target, IDependencyResolver dependencyResolver)
        {
            this.Target = target;
            this.dependencyResolver = dependencyResolver;
        }

        #region Implementation of IMessageBus

        public void Dispose()
        {
            // Nothing to dispose
        }

        #region Implementation of IPublishBus

        public Task Publish<TMessage>(TMessage message, string path = null, IDictionary<string, object> headers = null, CancellationToken cancellationToken = default)
            => Target.Publish(message, path: path, headers: headers, cancellationToken: cancellationToken, currentDependencyResolver: dependencyResolver);

        #endregion

        #region Implementation of IRequestResponseBus


        public Task<TResponseMessage> Send<TResponseMessage, TRequestMessage>(TRequestMessage request, CancellationToken cancellationToken)
            => Target.SendInternal<TResponseMessage>(request, timeout: null, path: null, headers: null, cancellationToken, currentDependencyResolver: dependencyResolver);

        public Task<TResponseMessage> Send<TResponseMessage>(IRequestMessage<TResponseMessage> request, CancellationToken cancellationToken)
            => Target.SendInternal<TResponseMessage>(request, timeout: null, path: null, headers: null, cancellationToken, currentDependencyResolver: dependencyResolver);

        public Task<TResponseMessage> Send<TResponseMessage>(IRequestMessage<TResponseMessage> request, string path = null, IDictionary<string, object> headers = null, CancellationToken cancellationToken = default)
            => Target.SendInternal<TResponseMessage>(request, timeout: null, path: path, headers: headers, cancellationToken, currentDependencyResolver: dependencyResolver);

        public Task<TResponseMessage> Send<TResponseMessage, TRequestMessage>(TRequestMessage request, string path = null, IDictionary<string, object> headers = null, CancellationToken cancellationToken = default)
            => Target.SendInternal<TResponseMessage>(request, timeout: null, path: path, headers: headers, cancellationToken, currentDependencyResolver: dependencyResolver);

        public Task<TResponseMessage> Send<TResponseMessage>(IRequestMessage<TResponseMessage> request, TimeSpan timeout, string path = null, IDictionary<string, object> headers = null, CancellationToken cancellationToken = default)
            => Target.SendInternal<TResponseMessage>(request, timeout: timeout, path: path, headers: headers, cancellationToken, currentDependencyResolver: dependencyResolver);

        #endregion

        #endregion
    }
}