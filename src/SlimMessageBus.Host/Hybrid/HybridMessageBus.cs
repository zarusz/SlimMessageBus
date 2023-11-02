namespace SlimMessageBus.Host.Hybrid;

public class HybridMessageBus : IMasterMessageBus, ICompositeMessageBus, IDisposable, IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, MessageBusBase> _busByName;
    private readonly ProducerByMessageTypeCache<MessageBusBase[]> _busesByMessageType;
    private readonly RuntimeTypeCache _runtimeTypeCache;

    public ILoggerFactory LoggerFactory { get; }
    public MessageBusSettings Settings { get; }
    public HybridMessageBusSettings ProviderSettings { get; }

    public bool IsStarted => _busByName.Values.All(x => x.IsStarted);

    public HybridMessageBus(MessageBusSettings settings, HybridMessageBusSettings providerSettings, MessageBusBuilder mbb)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        ProviderSettings = providerSettings ?? new HybridMessageBusSettings();

        // Try to resolve from DI, if also not available supress logging using the NullLoggerFactory
        LoggerFactory = (ILoggerFactory)settings.ServiceProvider?.GetService(typeof(ILoggerFactory)) ?? NullLoggerFactory.Instance;

        _logger = LoggerFactory.CreateLogger<HybridMessageBus>();

        _runtimeTypeCache = new RuntimeTypeCache();

        _busByName = new Dictionary<string, MessageBusBase>();
        foreach (var childBus in mbb.Children)
        {
            var bus = BuildBus(childBus.Value);
            _busByName.Add(bus.Settings.Name, bus);
        }

        var busesByMessageType = _busByName.Values
            .SelectMany(bus => bus.Settings.Producers.Select(p => (p.MessageType, Bus: bus)))
            .GroupBy(x => x.MessageType)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Bus).ToArray());

        var requestTypesWithMoreThanOneBus = busesByMessageType
            .Where(x => x.Value.Length > 1 && Array.Exists(x.Key.GetInterfaces(), i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>)))
            .Select(x => x.Key)
            .ToList();

        if (requestTypesWithMoreThanOneBus.Count > 0)
        {
            throw new ConfigurationMessageBusException($"Found request messages that are handled by more than one child bus: {string.Join(", ", requestTypesWithMoreThanOneBus)}. Double check your Produce configuration.");
        }

        _busesByMessageType = new ProducerByMessageTypeCache<MessageBusBase[]>(_logger, busesByMessageType, _runtimeTypeCache);

        // ToDo: defer start of busses until here
    }

    protected virtual MessageBusBase BuildBus(MessageBusBuilder builder)
    {
        var bus = builder.Build();

        return (MessageBusBase)bus;
    }

    public Task Start() =>
        Task.WhenAll(_busByName.Values.Select(x => x.Start()));

    public Task Stop() =>
        Task.WhenAll(_busByName.Values.Select(x => x.Stop()));

    #region Implementation of IDisposable and IAsyncDisposable

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeAsyncCore().ConfigureAwait(false).GetAwaiter().GetResult();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(disposing: false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Stops the consumers and disposes of internal bus objects.
    /// </summary>
    /// <returns></returns>
    protected async virtual ValueTask DisposeAsyncCore()
    {
        foreach (var bus in _busByName.Values)
        {
            await ((IAsyncDisposable)bus).DisposeSilently(() => $"Error disposing bus: {bus.Settings.Name}", _logger);
        }
        _busByName.Clear();
    }

    #endregion

    protected virtual MessageBusBase[] Route(object message, string path)
    {
        var messageType = message.GetType();

        var buses = _busesByMessageType[messageType]
            ?? throw new ConfigurationMessageBusException($"Could not find any bus that produces the message type: {messageType}");

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Resolved bus {BusName} for message type: {MessageType} and path {Path}", string.Join(",", buses.Select(x => x.Settings.Name)), messageType, path);
        }

        return buses;
    }

    #region Implementation of IMessageBusProducer

    public Task<TResponseMessage> ProduceSend<TResponseMessage>(object request, TimeSpan? timeout, string path = null, IDictionary<string, object> headers = null, IServiceProvider currentServiceProvider = null, CancellationToken cancellationToken = default)
    {
        var buses = Route(request, path);
        return buses[0].ProduceSend<TResponseMessage>(request, timeout, path, headers, currentServiceProvider, cancellationToken);
    }

    public async Task ProducePublish(object message, string path = null, IDictionary<string, object> headers = null, IServiceProvider currentServiceProvider = null, CancellationToken cancellationToken = default)
    {
        var buses = Route(message, path);

        if (buses.Length == 1)
        {
            await buses[0].ProducePublish(message, path, headers, currentServiceProvider, cancellationToken);
            return;
        }

        if (ProviderSettings.PublishExecutionMode == PublishExecutionMode.Parallel)
        {
            await Task.WhenAll(buses.Select(bus => bus.ProducePublish(message, path, headers, currentServiceProvider, cancellationToken)));
            return;
        }

        for (var i = 0; i < buses.Length; i++)
        {
            await buses[i].ProducePublish(message, path, headers, currentServiceProvider, cancellationToken);
        }
    }

    #endregion

    public Task ProvisionTopology() =>
        // Trigger provisioning to all child buses
        Task.WhenAll(_busByName.Values.Select(x => x.ProvisionTopology()));

    #region ICompositeMessageBus

    public IMessageBus GetChildBus(string name)
    {
        if (_busByName.TryGetValue(name, out var bus))
        {
            return bus;
        }
        return null;
    }

    public IEnumerable<IMessageBus> GetChildBuses() => _busByName.Values;

    #endregion

    #region Implementation of IPublishBus

    public async Task Publish<TMessage>(TMessage message, string path = null, IDictionary<string, object> headers = null, CancellationToken cancellationToken = default)
    {
        var buses = Route(message, path);

        if (buses.Length == 1)
        {
            await buses[0].Publish(message, path, headers, cancellationToken);
            return;
        }

        if (ProviderSettings.PublishExecutionMode == PublishExecutionMode.Parallel)
        {
            await Task.WhenAll(buses.Select(bus => bus.Publish(message, path, headers, cancellationToken)));
            return;
        }

        for (var i = 0; i < buses.Length; i++)
        {
            await buses[i].Publish(message, path, headers, cancellationToken);
        }
    }

    #endregion

    #region Implementation of IRequestResponseBus

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, string path = null, IDictionary<string, object> headers = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        var buses = Route(request, path);
        return buses[0].Send(request, path, headers, timeout, cancellationToken);
    }

    public Task Send(IRequest request, string path = null, IDictionary<string, object> headers = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        var buses = Route(request, path);
        return buses[0].Send(request, path, headers, timeout, cancellationToken);
    }

    public Task<TResponse> Send<TResponse, TRequest>(TRequest request, string path = null, IDictionary<string, object> headers = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        var buses = Route(request, path);
        return buses[0].Send<TResponse, TRequest>(request, path, headers, timeout, cancellationToken);
    }

    #endregion
}
