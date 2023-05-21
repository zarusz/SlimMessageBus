namespace SlimMessageBus.Host;

public class MessageBusBuilder
{
    /// <summary>
    /// Parent bus builder.
    /// </summary>
    public MessageBusBuilder Parent { get; private set; }

    /// <summary>
    /// Declared child buses.
    /// </summary>
    public IDictionary<string, MessageBusBuilder> Children { get; } = new Dictionary<string, MessageBusBuilder>();

    /// <summary>
    /// The current settings that are being built.
    /// </summary>
    public MessageBusSettings Settings { get; private set; } = new();

    /// <summary>
    /// The bus factory method.
    /// </summary>
    public Func<MessageBusSettings, IMessageBus> BusFactory { get; private set; }

    public IList<Action<IServiceCollection>> PostConfigurationActions { get; } = new List<Action<IServiceCollection>>();

    protected MessageBusBuilder()
    {
    }

    protected MessageBusBuilder(MessageBusBuilder other)
    {
        Settings = other.Settings;
        Children = other.Children;
        BusFactory = other.BusFactory;
        PostConfigurationActions = other.PostConfigurationActions;
    }

    public static MessageBusBuilder Create() => new();

    public MessageBusBuilder MergeFrom(MessageBusSettings settings)
    {
        if (settings is null) throw new ArgumentNullException(nameof(settings));

        Settings.MergeFrom(settings);
        return this;
    }

    /// <summary>
    /// Configures (declares) the production (publishing for pub/sub or request sending in request/response) of a message 
    /// </summary>
    /// <typeparam name="T">Type of the message</typeparam>
    /// <param name="builder"></param>
    /// <returns></returns>
    public MessageBusBuilder Produce<T>(Action<ProducerBuilder<T>> builder)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));

        var item = new ProducerSettings();
        builder(new ProducerBuilder<T>(item));
        Settings.Producers.Add(item);
        return this;
    }

    /// <summary>
    /// Configures (declares) the production (publishing for pub/sub or request sending in request/response) of a message 
    /// </summary>
    /// <param name="messageType">Type of the message</param>
    /// <param name="builder"></param>
    /// <returns></returns>
    public MessageBusBuilder Produce(Type messageType, Action<ProducerBuilder<object>> builder)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));

        var item = new ProducerSettings();
        builder(new ProducerBuilder<object>(item, messageType));
        Settings.Producers.Add(item);
        return this;
    }

    /// <summary>
    /// Configures (declares) the consumer of given message types in pub/sub or queue communication.
    /// </summary>
    /// <typeparam name="TMessage">Type of message</typeparam>
    /// <param name="builder"></param>
    /// <returns></returns>
    public MessageBusBuilder Consume<TMessage>(Action<ConsumerBuilder<TMessage>> builder)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));

        var b = new ConsumerBuilder<TMessage>(Settings);
        builder(b);

        if (b.ConsumerSettings.ConsumerType is null)
        {
            // Apply default consumer type of not set
            b.WithConsumer<IConsumer<TMessage>>();
        }
        return this;
    }

    /// <summary>
    /// Configures (declares) the consumer of given message types in pub/sub or queue communication.
    /// </summary>
    /// <param name="messageType">Type of message</param>
    /// <param name="builder"></param>
    /// <returns></returns>
    public MessageBusBuilder Consume(Type messageType, Action<ConsumerBuilder<object>> builder)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));

        builder(new ConsumerBuilder<object>(Settings, messageType));
        return this;
    }

    /// <summary>
    /// Configures (declares) the handler of a given request message type in request-response communication.
    /// </summary>
    /// <typeparam name="TRequest"></typeparam>
    /// <typeparam name="TResponse"></typeparam>
    /// <param name="builder"></param>
    /// <returns></returns>
    public MessageBusBuilder Handle<TRequest, TResponse>(Action<HandlerBuilder<TRequest, TResponse>> builder)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));

        var b = new HandlerBuilder<TRequest, TResponse>(Settings);
        builder(b);

        if (b.ConsumerSettings.ConsumerType is null)
        {
            // Apply default handler type of not set
            b.WithHandler<IRequestHandler<TRequest, TResponse>>();
        }

        return this;
    }

    /// <summary>
    /// Configures (declares) the handler of a given request message type which nas no response message type.
    /// </summary>
    /// <typeparam name="TRequest"></typeparam>
    /// <param name="builder"></param>
    /// <returns></returns>
    public MessageBusBuilder Handle<TRequest>(Action<HandlerBuilder<TRequest>> builder)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));

        var b = new HandlerBuilder<TRequest>(Settings);
        builder(b);

        if (b.ConsumerSettings.ConsumerType is null)
        {
            // Apply default handler type of not set
            b.WithHandler<IRequestHandler<TRequest>>();
        }

        return this;
    }

    /// <summary>
    /// Configures (declares) the handler of a given request message type in request-response communication.
    /// </summary>
    /// <typeparam name="TRequest"></typeparam>
    /// <typeparam name="TResponse"></typeparam>
    /// <param name="builder"></param>
    /// <returns></returns>
    public MessageBusBuilder Handle(Type requestType, Type responseType, Action<HandlerBuilder<object, object>> builder)
    {
        if (requestType == null) throw new ArgumentNullException(nameof(requestType));
        if (responseType == null) throw new ArgumentNullException(nameof(responseType));
        if (builder == null) throw new ArgumentNullException(nameof(builder));

        builder(new HandlerBuilder<object, object>(Settings, requestType, responseType));
        return this;
    }

    /// <summary>
    /// Configures (declares) the handler of a given request message type (for which there is no response type) in request-response communication.
    /// </summary>
    /// <typeparam name="TRequest"></typeparam>
    /// <typeparam name="TResponse"></typeparam>
    /// <param name="builder"></param>
    /// <returns></returns>
    public MessageBusBuilder Handle(Type requestType, Action<HandlerBuilder<object>> builder)
    {
        if (requestType == null) throw new ArgumentNullException(nameof(requestType));
        if (builder == null) throw new ArgumentNullException(nameof(builder));

        builder(new HandlerBuilder<object>(Settings, requestType));
        return this;
    }

    public MessageBusBuilder ExpectRequestResponses(Action<RequestResponseBuilder> reqRespBuilder)
    {
        if (reqRespBuilder == null) throw new ArgumentNullException(nameof(reqRespBuilder));

        var item = new RequestResponseSettings();
        reqRespBuilder(new RequestResponseBuilder(item));
        Settings.RequestResponse = item;
        return this;
    }

    /// <summary>
    /// Serializer type (<see cref="IMessageSerializer"/>) to look up in the DI for this bus.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public MessageBusBuilder WithSerializer<T>() where T : IMessageSerializer => WithSerializer(typeof(T));

    /// <summary>
    /// Serializer type (<see cref="IMessageSerializer"/>) to look up in the DI for this bus.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public MessageBusBuilder WithSerializer(Type serializerType)
    {
        if (serializerType is not null && !typeof(IMessageSerializer).IsAssignableFrom(serializerType))
        {
            throw new ConfigurationMessageBusException($"The serializer type {serializerType.FullName} does not implement the interface {nameof(IMessageSerializer)}");
        }

        Settings.SerializerType = serializerType ?? throw new ArgumentNullException(nameof(serializerType));
        return this;
    }

    public MessageBusBuilder WithDependencyResolver(IServiceProvider serviceProvider)
    {
        Settings.ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        return this;
    }

    public MessageBusBuilder WithProvider(Func<MessageBusSettings, IMessageBus> provider)
    {
        BusFactory = provider ?? throw new ArgumentNullException(nameof(provider));
        return this;
    }

    public MessageBusBuilder Do(Action<MessageBusBuilder> builder)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));

        builder(this);
        return this;
    }

    /// <summary>
    /// Sets the default enable (or disable) creation of DI child scope for each meesage.
    /// </summary>
    /// <param name="enabled"></param>
    /// <returns></returns>
    public MessageBusBuilder PerMessageScopeEnabled(bool enabled)
    {
        Settings.IsMessageScopeEnabled = enabled;
        return this;
    }

    public MessageBusBuilder WithMessageTypeResolver(Type messageTypeResolverType)
    {
        Settings.MessageTypeResolverType = messageTypeResolverType ?? throw new ArgumentNullException(nameof(messageTypeResolverType));
        return this;
    }

    public MessageBusBuilder WithMessageTypeResolver<T>() => WithMessageTypeResolver(typeof(T));

    /// <summary>
    /// Hook called whenver message is being produced. Can be used to add (or mutate) message headers.
    /// </summary>
    public MessageBusBuilder WithHeaderModifier(MessageHeaderModifier<object> headerModifier) => WithHeaderModifier<object>(headerModifier);

    /// <summary>
    /// Hook called whenver message is being produced. Can be used to add (or mutate) message headers.
    /// </summary>
    public MessageBusBuilder WithHeaderModifier<T>(MessageHeaderModifier<T> headerModifier)
    {
        if (headerModifier == null) throw new ArgumentNullException(nameof(headerModifier));

        Settings.HeaderModifier = (headers, message) =>
        {
            if (message is T typedMessage)
            {
                headerModifier(headers, typedMessage);
            }
        };
        return this;
    }

    /// <summary>
    /// Enables or disabled the auto statrt of message consumption upon bus creation. If false, then you need to call the .Start() on the bus to start consuming messages.
    /// </summary>
    /// <param name="enabled"></param>
    public MessageBusBuilder AutoStartConsumersEnabled(bool enabled)
    {
        Settings.AutoStartConsumers = enabled;
        return this;
    }

    public MessageBusBuilder AddChildBus(string busName, Action<MessageBusBuilder> builderAction)
    {
        if (busName is null) throw new ArgumentNullException(nameof(busName));
        if (builderAction is null) throw new ArgumentNullException(nameof(builderAction));

        if (!Children.TryGetValue(busName, out var child))
        {
            child = Create();
            child.Settings = new MessageBusSettings(Settings)
            {
                Name = busName
            };

            child.Parent = this;
            Children.Add(busName, child);

            child.MergeFrom(Settings);
        }

        builderAction?.Invoke(child);

        return this;
    }

    public IMessageBus Build()
    {
        if (BusFactory is null)
        {
            var busName = Settings.Name != null ? $"Child bus [{Settings.Name}]: " : string.Empty;
            throw new ConfigurationMessageBusException($"{busName}The bus provider was not configured. Check the MessageBus configuration and ensure the has the '.WithProviderXxx()' setting for one of the available transports.");
        }
        return BusFactory(Settings);
    }
}