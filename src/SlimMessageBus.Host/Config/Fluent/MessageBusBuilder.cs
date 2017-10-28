using System;

namespace SlimMessageBus.Host.Config
{
    public class MessageBusBuilder
    {
        private readonly MessageBusSettings _settings = new MessageBusSettings();
        private Func<MessageBusSettings, IMessageBus> _factory; 

        public MessageBusBuilder Publish<T>(Action<PublisherBuilder<T>> publisherBuilder)
        {
            var item = new PublisherSettings();
            publisherBuilder(new PublisherBuilder<T>(item));
            _settings.Publishers.Add(item);
            return this;
        }

        public MessageBusBuilder SubscribeTo<TMessage>(Action<SubscriberBuilder<TMessage>> subscriberBuilder)
        {
            subscriberBuilder(new SubscriberBuilder<TMessage>(_settings));
            return this;
        }

        public MessageBusBuilder Handle<TRequest, TResponse>(Action<HandlerBuilder<TRequest, TResponse>> handlerBuilder)
            where TRequest: IRequestMessage<TResponse>
        {
            handlerBuilder(new HandlerBuilder<TRequest, TResponse>(_settings));
            return this;
        }

        public MessageBusBuilder ExpectRequestResponses(Action<RequestResponseBuilder> reqRespBuilder)
        {
            var item = new RequestResponseSettings();
            reqRespBuilder(new RequestResponseBuilder(item));
            _settings.RequestResponse = item;
            return this;
        }

        public MessageBusBuilder WithSerializer(IMessageSerializer serializer)
        {
            _settings.Serializer = serializer;
            return this;
        }

        public MessageBusBuilder WithDependencyResolver(IDependencyResolver dependencyResolver)
        {
            _settings.DependencyResolver = dependencyResolver;
            return this;
        }

        public MessageBusBuilder WithProvider(Func<MessageBusSettings, IMessageBus> provider)
        {
            _factory = provider;
            return this;
        }

        public IMessageBus Build()
        {
            return _factory(_settings);
        }
    }
}