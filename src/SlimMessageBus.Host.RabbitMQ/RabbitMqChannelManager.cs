namespace SlimMessageBus.Host.RabbitMQ;

using Microsoft.Extensions.Logging;
using global::RabbitMQ.Client;

/// <summary>
/// Manages RabbitMQ channel connection, reconnection, retry logic, and background timer.
/// Acts as an IModel wrapper with automatic connection recovery.
/// </summary>
internal class RabbitMqChannelManager : IRabbitMqChannel, IDisposable
{
    private readonly ILogger _logger;
    private readonly RabbitMqMessageBusSettings _providerSettings;
    private readonly MessageBusSettings _settings;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Func<bool> _isDisposingOrDisposed;

    private IConnection _connection;
    private IModel _channel;
    private readonly object _channelLock = new();
    
    private readonly Timer _connectionRetryTimer;
    private readonly TimeSpan _connectionRetryInterval;
    private volatile bool _isConnecting;

    public IModel Channel => _channel;
    public object ChannelLock => _channelLock;

    public RabbitMqChannelManager(
        ILoggerFactory loggerFactory,
        RabbitMqMessageBusSettings providerSettings,
        MessageBusSettings settings,
        Func<bool> isDisposingOrDisposed)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _providerSettings = providerSettings ?? throw new ArgumentNullException(nameof(providerSettings));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _isDisposingOrDisposed = isDisposingOrDisposed ?? throw new ArgumentNullException(nameof(isDisposingOrDisposed));

        _logger = _loggerFactory.CreateLogger<RabbitMqChannelManager>();

        // Set up connection retry interval (default to NetworkRecoveryInterval or 10 seconds)
        var networkRecoveryInterval = _providerSettings.ConnectionFactory.NetworkRecoveryInterval;
        _connectionRetryInterval = networkRecoveryInterval == TimeSpan.Zero ? TimeSpan.FromSeconds(10) : networkRecoveryInterval;
        
        // Initialize the retry timer (but don't start it yet)
        _connectionRetryTimer = new Timer(OnConnectionRetryTimer, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// Initializes the connection with retry logic.
    /// </summary>
    public async Task InitializeConnection(CancellationToken cancellationToken = default)
    {
        const int initialRetryCount = 3;
        var connectionEstablished = false;

        try
        {
            await Retry.WithDelay(
                operation: (ct) =>
                {
                    connectionEstablished = EstablishConnection();
                    return Task.CompletedTask;
                },
                shouldRetry: (ex, attempt) =>
                {
                    if (ex is global::RabbitMQ.Client.Exceptions.BrokerUnreachableException && attempt < initialRetryCount)
                    {
                        _logger.LogInformation(ex, "Retrying {Retry} of {RetryCount} connection to RabbitMQ...", attempt, initialRetryCount);
                        return true;
                    }
                    return false;
                },
                delay: _connectionRetryInterval,
                cancellationToken: cancellationToken
            );

            if (connectionEstablished)
            {
                _logger.LogInformation("RabbitMQ connection established successfully");
                // Stop retry timer if connection was successful
                _connectionRetryTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not initialize RabbitMQ connection after {RetryCount} attempts: {ErrorMessage}", initialRetryCount, e.Message);
            
            // Start continuous retry mechanism
            StartConnectionRetryTimer();
        }
    }

    /// <summary>
    /// Ensures the channel is available, triggering reconnection if necessary.
    /// </summary>
    public void EnsureChannel()
    {
        if (_channel == null)
        {
            // If channel is null, trigger immediate reconnection attempt
            if (!_isConnecting)
            {
                _logger.LogWarning("Channel is not available, attempting immediate reconnection");
                Task.Run(() => OnConnectionRetryTimer(null));
            }
            
            throw new ProducerMessageBusException("The Channel is not available at this time");
        }
    }

    /// <summary>
    /// Executes an action with the channel, ensuring it's available and handling the lock.
    /// </summary>
    public void ExecuteWithChannel(Action<IModel> action)
    {
        EnsureChannel();
        
        lock (_channelLock)
        {
            action(_channel);
        }
    }

    /// <summary>
    /// Executes a function with the channel, ensuring it's available and handling the lock.
    /// </summary>
    public T ExecuteWithChannel<T>(Func<IModel, T> func)
    {
        EnsureChannel();
        
        lock (_channelLock)
        {
            return func(_channel);
        }
    }

    private bool EstablishConnection()
    {
        try
        {
            // See https://www.rabbitmq.com/client-libraries/dotnet-api-guide#connection-recovery
            _providerSettings.ConnectionFactory.AutomaticRecoveryEnabled = true;
            _providerSettings.ConnectionFactory.DispatchConsumersAsync = true;

            // Set up connection recovery event handlers
            var newConnection = _providerSettings.Endpoints != null && _providerSettings.Endpoints.Count > 0
                ? _providerSettings.ConnectionFactory.CreateConnection(_providerSettings.Endpoints)
                : _providerSettings.ConnectionFactory.CreateConnection();

            // Subscribe to connection shutdown events for better resilience
            newConnection.ConnectionShutdown += OnConnectionShutdown;

            lock (_channelLock)
            {
                // Clean up existing connection
                if (_connection != null)
                {
                    _connection.ConnectionShutdown -= OnConnectionShutdown;
                }

                CloseAndDisposeChannel(_channel);
                CloseAndDisposeConnection(_connection);

                _connection = newConnection;

                if (_connection != null)
                {
                    _channel = _connection.CreateModel();

                    // Provision topology
                    ProvisionTopology();
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to establish RabbitMQ connection: {ErrorMessage}", ex.Message);
            return false;
        }
    }

    private void ProvisionTopology()
    {
        if (_channel == null) return;

        try
        {
            var topologyService = new RabbitMqTopologyService(_loggerFactory, _channel, _settings, _providerSettings);

            var customAction = _providerSettings.GetOrDefault(RabbitMqProperties.TopologyInitializer);
            if (customAction != null)
            {
                // Allow the user to specify its own initializer
                customAction(_channel, () => topologyService.ProvisionTopology());
            }
            else
            {
                // Perform default topology setup
                topologyService.ProvisionTopology();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to provision RabbitMQ topology: {ErrorMessage}", ex.Message);
            throw;
        }
    }

    private void OnConnectionShutdown(object sender, ShutdownEventArgs e)
    {
        if (!_isDisposingOrDisposed())
        {
            _logger.LogWarning("RabbitMQ connection shutdown detected. Reason: {Reason}, Initiator: {Initiator}", 
                e.ReplyText, e.Initiator);
            
            // Start retry timer if not already running
            StartConnectionRetryTimer();
        }
    }

    private void StartConnectionRetryTimer()
    {
        if (!_isConnecting && !_isDisposingOrDisposed())
        {
            _logger.LogInformation("Starting RabbitMQ connection retry timer with interval: {Interval}", _connectionRetryInterval);
            _connectionRetryTimer.Change(_connectionRetryInterval, _connectionRetryInterval);
        }
    }

    private void OnConnectionRetryTimer(object state)
    {
        if (_isConnecting || _isDisposingOrDisposed())
            return;

        // Check if connection is already healthy
        lock (_channelLock)
        {
            if (_connection?.IsOpen == true && _channel?.IsOpen == true)
            {
                _logger.LogDebug("RabbitMQ connection is healthy, stopping retry timer");
                _connectionRetryTimer.Change(Timeout.Infinite, Timeout.Infinite);
                return;
            }
        }

        _isConnecting = true;
        try
        {
            _logger.LogInformation("Attempting to reconnect to RabbitMQ...");
            
            var success = EstablishConnection();
            if (success)
            {
                _logger.LogInformation("RabbitMQ reconnection successful");
                _connectionRetryTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            else
            {
                _logger.LogWarning("RabbitMQ reconnection failed, will retry in {Interval}", _connectionRetryInterval);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during RabbitMQ reconnection attempt: {ErrorMessage}", ex.Message);
        }
        finally
        {
            _isConnecting = false;
        }
    }

    /// <summary>
    /// Safely closes and disposes a RabbitMQ channel.
    /// </summary>
    private static void CloseAndDisposeChannel(IModel channel)
    {
        if (channel != null)
        {
            if (!channel.IsClosed)
            {
                channel.Close();
            }
            channel.Dispose();
        }
    }

    /// <summary>
    /// Safely closes and disposes a RabbitMQ connection.
    /// </summary>
    private static void CloseAndDisposeConnection(IConnection connection)
    {
        if (connection != null)
        {
            connection.Close();
            connection.Dispose();
        }
    }

    public void Dispose()
    {
        // Stop the retry timer
        _connectionRetryTimer?.Dispose();

        if (_channel != null)
        {
            CloseAndDisposeChannel(_channel);
            _channel = null;
        }

        if (_connection != null)
        {
            // Unsubscribe from events before disposing
            _connection.ConnectionShutdown -= OnConnectionShutdown;
            
            CloseAndDisposeConnection(_connection);
            _connection = null;
        }

        GC.SuppressFinalize(this);
    }
}