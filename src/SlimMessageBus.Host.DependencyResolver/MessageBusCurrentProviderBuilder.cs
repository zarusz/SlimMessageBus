namespace SlimMessageBus.Host.DependencyResolver
{
    using System;

    /// <summary>
    /// Builder class to fluently configure the <see cref="MessageBus.SetProvider"/> provider
    /// </summary>
    public class MessageBusCurrentProviderBuilder
    {
        private Func<IMessageBus> _provider;

        protected MessageBusCurrentProviderBuilder()
        {            
        }

        public static MessageBusCurrentProviderBuilder Create()
        {
            return new MessageBusCurrentProviderBuilder();
        }

        public void SetProvider(Func<IMessageBus> provider)
        {
            _provider = provider;
        }

        public MessageBusCurrentProviderBuilder FromSingleton(IMessageBus bus)
        {
            SetProvider(() => bus);
            return this;
        }

        public MessageBusCurrentProviderBuilder From(IDependencyResolver dependencyResolver)
        {
            SetProvider(() => (IMessageBus)dependencyResolver.Resolve(typeof(IMessageBus)));
            return this;
        }

        public Func<IMessageBus> Build()
        {
            return _provider;
        }
    }
}
