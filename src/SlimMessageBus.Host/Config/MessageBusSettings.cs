namespace SlimMessageBus.Host.Config;

public class MessageBusSettings : HasProviderExtensions, IBusEvents
{
    /// <summary>
    /// The bus name.
    /// </summary>
    public string Name { get; set; }
    public ILoggerFactory LoggerFactory { get; set; }
    public IList<ProducerSettings> Producers { get; }
    public IList<ConsumerSettings> Consumers { get; }
    public RequestResponseSettings RequestResponse { get; set; }
    public IMessageSerializer Serializer { get; set; }
    public IServiceProvider ServiceProvider { get; set; }
    public IMessageTypeResolver MessageTypeResolver { get; set; }

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

    public MessageBusSettings()
    {
        Producers = new List<ProducerSettings>();
        Consumers = new List<ConsumerSettings>();
        MessageTypeResolver = new AssemblyQualifiedNameMessageTypeResolver();
        AutoStartConsumers = true;
    }

    public virtual void MergeFrom(MessageBusSettings settings)
    {
        if (settings is null) throw new ArgumentNullException(nameof(settings));

        if (Name == null && settings.Name != null)
        {
            Name = settings.Name;
        }

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

        if (ServiceProvider == null && settings.ServiceProvider != null)
        {
            ServiceProvider = settings.ServiceProvider;
        }

        if (MessageTypeResolver == null && settings.MessageTypeResolver != null)
        {
            MessageTypeResolver = settings.MessageTypeResolver;
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