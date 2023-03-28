namespace SlimMessageBus.Host;

public class MessageBusSettings : HasProviderExtensions, IBusEvents
{
    private IServiceProvider _serviceProvider;
    private readonly IList<MessageBusSettings> _children;

    public MessageBusSettings Parent { get; }
    public IEnumerable<MessageBusSettings> Children => _children;

    public IServiceProvider ServiceProvider
    {
        get => _serviceProvider ?? Parent?.ServiceProvider;
        set => _serviceProvider = value;
    }

    /// <summary>
    /// The bus name.
    /// </summary>
    public string Name { get; set; }
    public IList<ProducerSettings> Producers { get; }
    public IList<ConsumerSettings> Consumers { get; }
    public RequestResponseSettings RequestResponse { get; set; }
    public Type SerializerType { get; set; }
    public Type MessageTypeResolverType { get; set; }

    #region Implementation of IConsumerEvents
    ///
    /// <inheritdoc/>
    ///
    public Action<IMessageBus, AbstractConsumerSettings, object, string, object> OnMessageArrived { get; set; }
    ///
    /// <inheritdoc/>
    ///
    public Action<IMessageBus, AbstractConsumerSettings, object, string, object> OnMessageFinished { get; set; }
    ///
    /// <inheritdoc/>
    ///
    public Action<IMessageBus, AbstractConsumerSettings, object, object> OnMessageExpired { get; set; }
    ///
    /// <inheritdoc/>
    ///
    public Action<IMessageBus, AbstractConsumerSettings, object, Exception, object> OnMessageFault { get; set; }
    #endregion

    #region Implementation of IProducerEvents

    public Action<IMessageBus, ProducerSettings, object, string> OnMessageProduced { get; set; }

    #endregion

    /// <summary>
    /// Determines if a child scope is created for the message consumption. The consumer instance is then derived from that scope.
    /// </summary>
    public bool? IsMessageScopeEnabled { get; set; }

    /// <summary>
    /// Hook called whenver message is being produced. Can be used to add (or mutate) message headers.
    /// </summary>
    public Action<IDictionary<string, object>, object> HeaderModifier { get; set; }

    /// <summary>
    /// When true will start the message consumption on consumers after the bus is created.
    /// </summary>
    public bool AutoStartConsumers { get; set; }

    public MessageBusSettings(MessageBusSettings parent = null)
    {
        _children = new List<MessageBusSettings>();
        Producers = new List<ProducerSettings>();
        Consumers = new List<ConsumerSettings>();
        SerializerType = typeof(IMessageSerializer);
        AutoStartConsumers = true;

        if (parent != null)
        {
            Parent = parent;
            parent._children.Add(this);
        }
    }

    public virtual void MergeFrom(MessageBusSettings settings)
    {
        if (settings is null) throw new ArgumentNullException(nameof(settings));

        if (Name == null && settings.Name != null)
        {
            Name = settings.Name;
        }

        if (SerializerType == null && settings.SerializerType != null)
        {
            SerializerType = settings.SerializerType;
        }

        if (MessageTypeResolverType == null && settings.MessageTypeResolverType != null)
        {
            MessageTypeResolverType = settings.MessageTypeResolverType;
        }

        if (ServiceProvider == null && settings.ServiceProvider != null)
        {
            ServiceProvider = settings.ServiceProvider;
        }

        if (OnMessageArrived == null && settings.OnMessageArrived != null)
        {
            OnMessageArrived = settings.OnMessageArrived;
        }

        if (OnMessageFinished == null && settings.OnMessageFinished != null)
        {
            OnMessageFinished = settings.OnMessageFinished;
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

        if (HeaderModifier == null && settings.HeaderModifier != null)
        {
            HeaderModifier = settings.HeaderModifier;
        }

        AutoStartConsumers = settings.AutoStartConsumers;
    }
}