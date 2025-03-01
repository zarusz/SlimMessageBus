namespace SlimMessageBus.Host.Hybrid;

using System.Collections.Concurrent;

using SlimMessageBus.Host.Serialization;

public partial class HybridMessageBus : IMasterMessageBus, ICompositeMessageBus, IDisposable, IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, MessageBusBase> _busByName;
    private readonly ProducerByMessageTypeCache<MessageBusBase[]> _busesByMessageType;
    private readonly ConcurrentDictionary<Type, bool> _undeclaredMessageType;

    public ILoggerFactory LoggerFactory { get; }
    public MessageBusSettings Settings { get; }
    public HybridMessageBusSettings ProviderSettings { get; }

    public string Name => Settings.Name;

    public bool IsStarted => _busByName.Values.All(x => x.IsStarted);

    public IMessageSerializerProvider SerializerProvider => Settings.GetSerializerProvider(Settings.ServiceProvider);

    public HybridMessageBus(MessageBusSettings settings, HybridMessageBusSettings providerSettings, MessageBusBuilder mbb)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        ProviderSettings = providerSettings ?? new HybridMessageBusSettings();

        // Try to resolve from DI. If not available, suppress logging by using the NullLoggerFactory
        LoggerFactory = (ILoggerFactory)settings.ServiceProvider?.GetService(typeof(ILoggerFactory)) ?? NullLoggerFactory.Instance;

        _logger = LoggerFactory.CreateLogger<HybridMessageBus>();

        _busByName = [];
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

        var runtimeTypeCache = settings.ServiceProvider.GetRequiredService<RuntimeTypeCache>();
        _busesByMessageType = new ProducerByMessageTypeCache<MessageBusBase[]>(_logger, busesByMessageType, runtimeTypeCache);

        _undeclaredMessageType = new();

        // ToDo: defer start of buses until here
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

        var buses = _busesByMessageType[messageType];
        if (buses != null)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                var busName = buses.JoinOrSingle(x => x.Settings.Name);
                LogResolvedBus(path, messageType, busName);
            }
            return buses;
        }

        if (ProviderSettings.UndeclaredMessageTypeMode == UndeclaredMessageTypeMode.RaiseException)
        {
            throw new ConfigurationMessageBusException($"Could not find any bus that produces the message type: {messageType}");
        }

        // Add the message type, so that we only emit warn log once
        if (ProviderSettings.UndeclaredMessageTypeMode == UndeclaredMessageTypeMode.RaiseOneTimeLog && _undeclaredMessageType.TryAdd(messageType, true))
        {
            LogCouldNotFindBus(messageType);
        }

        return [];
    }

    #region Implementation of IMessageBusProducer

    public Task<TResponseMessage> ProduceSend<TResponseMessage>(object request, string path = null, IDictionary<string, object> headers = null, TimeSpan? timeout = null, IMessageBusTarget targetBus = null, CancellationToken cancellationToken = default)
    {
        var buses = Route(request, path);
        if (buses.Length > 0)
        {
            return buses[0].ProduceSend<TResponseMessage>(request, path, headers, timeout, targetBus, cancellationToken);
        }
        return Task.FromResult<TResponseMessage>(default);
    }

    public async Task ProducePublish(object message, string path = null, IDictionary<string, object> headers = null, IMessageBusTarget targetBus = null, CancellationToken cancellationToken = default)
    {
        var buses = Route(message, path);
        if (buses.Length == 0)
        {
            return;
        }

        if (buses.Length == 1)
        {
            await buses[0].ProducePublish(message, path, headers, targetBus, cancellationToken);
            return;
        }

        if (ProviderSettings.PublishExecutionMode == PublishExecutionMode.Parallel)
        {
            await Task.WhenAll(buses.Select(bus => bus.ProducePublish(message, path, headers, targetBus, cancellationToken)));
            return;
        }

        for (var i = 0; i < buses.Length; i++)
        {
            await buses[i].ProducePublish(message, path, headers, targetBus, cancellationToken);
        }
    }

    #endregion

    public Task ProvisionTopology() =>
        // Trigger provisioning to all child buses
        Task.WhenAll(_busByName.Values.Select(x => x.ProvisionTopology()));

    #region ICompositeMessageBus

    public IMasterMessageBus GetChildBus(string name)
    {
        if (_busByName.TryGetValue(name, out var bus))
        {
            return bus;
        }
        return null;
    }

    public IEnumerable<IMasterMessageBus> GetChildBuses() => _busByName.Values;

    #endregion

    #region Logging 

    [LoggerMessage(
       EventId = 0,
       Level = LogLevel.Debug,
       Message = "Resolved bus {BusName} for message type {MessageType} and path {Path}")]
    private partial void LogResolvedBus(string path, Type messageType, string busName);

    [LoggerMessage(
       EventId = 1,
       Level = LogLevel.Information,
       Message = "Could not find any bus that produces the message type {MessageType}. Messages of that type will not be delivered to any child bus. Double check the message bus configuration.")]
    private partial void LogCouldNotFindBus(Type messageType);

    #endregion
}

#if NETSTANDARD2_0

public partial class HybridMessageBus
{
    private partial void LogResolvedBus(string path, Type messageType, string busName)
        => _logger.LogDebug("Resolved bus {BusName} for message type {MessageType} and path {Path}", busName, messageType, path);

    private partial void LogCouldNotFindBus(Type messageType)
        => _logger.LogInformation("Could not find any bus that produces the message type {MessageType}. Messages of that type will not be delivered to any child bus. Double check the message bus configuration.", messageType);
}

#endif