namespace SlimMessageBus.Host.RabbitMQ;

using global::RabbitMQ.Client;

using Microsoft.Extensions.Logging;

/// <summary>
/// Manages RabbitMQ channel connection, reconnection, retry logic, and background timer.
/// Acts as an IModel wrapper with automatic connection recovery.
/// </summary>
internal partial class RabbitMqChannelManager : IRabbitMqChannel, IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly RabbitMqMessageBusSettings _providerSettings;
    private readonly MessageBusSettings _settings;
    private readonly Func<bool> _isDisposingOrDisposed;

    private IConnection _connection;
    private IModel _channel;
    private readonly object _channelLock = new();

    private readonly Timer _connectionRetryTimer;
    private readonly TimeSpan _connectionRetryInterval;
    private volatile bool _isConnecting;
    private bool _disposed;

    public IModel Channel => _channel;
    public object ChannelLock => _channelLock;

    public RabbitMqChannelManager(
        ILoggerFactory loggerFactory,
        RabbitMqMessageBusSettings providerSettings,
        MessageBusSettings settings,
        Func<bool> isDisposingOrDisposed)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = loggerFactory.CreateLogger<RabbitMqChannelManager>();
        _providerSettings = providerSettings ?? throw new ArgumentNullException(nameof(providerSettings));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _isDisposingOrDisposed = isDisposingOrDisposed ?? throw new ArgumentNullException(nameof(isDisposingOrDisposed));


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
                        LogRetryingConnection(attempt, initialRetryCount, ex);
                        return true;
                    }
                    return false;
                },
                delay: _connectionRetryInterval,
                cancellationToken: cancellationToken
            );

            if (connectionEstablished)
            {
                LogConnectionEstablishedSuccessfully();
                // Stop retry timer if connection was successful
                _connectionRetryTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }
        catch (Exception e)
        {
            LogCouldNotInitializeConnection(initialRetryCount, e.Message, e);

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
                LogChannelNotAvailableAttemptingReconnection();
                Task.Run(() => OnConnectionRetryTimer(null));
            }

            throw new ProducerMessageBusException("The Channel is not available at this time");
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
            LogFailedToEstablishConnection(ex.Message, ex);
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
            LogFailedToProvisionTopology(ex.Message, ex);
            throw;
        }
    }

    private void OnConnectionShutdown(object sender, ShutdownEventArgs e)
    {
        if (!_isDisposingOrDisposed())
        {
            LogConnectionShutdownDetected(e.ReplyText, e.Initiator);

            // Start retry timer if not already running
            StartConnectionRetryTimer();
        }
    }

    private void StartConnectionRetryTimer()
    {
        if (!_isConnecting && !_isDisposingOrDisposed())
        {
            LogStartingConnectionRetryTimer(_connectionRetryInterval);
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
                LogConnectionIsHealthy();
                _connectionRetryTimer.Change(Timeout.Infinite, Timeout.Infinite);
                return;
            }
        }

        _isConnecting = true;
        try
        {
            LogAttemptingToReconnect();

            var success = EstablishConnection();
            if (success)
            {
                LogReconnectionSuccessful();
                _connectionRetryTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            else
            {
                LogReconnectionFailed(_connectionRetryInterval);
            }
        }
        catch (Exception ex)
        {
            LogErrorDuringReconnectionAttempt(ex.Message, ex);
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

    #region Disposable

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
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
        }
        // Free unmanaged resources (none in this class)
        _disposed = true;
    }

    #endregion

    #region Logging

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Retrying {Retry} of {RetryCount} connection to RabbitMQ...")]
    private partial void LogRetryingConnection(int retry, int retryCount, Exception ex);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "RabbitMQ connection established successfully")]
    private partial void LogConnectionEstablishedSuccessfully();

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Could not initialize RabbitMQ connection after {RetryCount} attempts: {ErrorMessage}")]
    private partial void LogCouldNotInitializeConnection(int retryCount, string errorMessage, Exception ex);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Channel is not available, attempting immediate reconnection")]
    private partial void LogChannelNotAvailableAttemptingReconnection();

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "Failed to establish RabbitMQ connection: {ErrorMessage}")]
    private partial void LogFailedToEstablishConnection(string errorMessage, Exception ex);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Error,
        Message = "Failed to provision RabbitMQ topology: {ErrorMessage}")]
    private partial void LogFailedToProvisionTopology(string errorMessage, Exception ex);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Warning,
        Message = "RabbitMQ connection shutdown detected. Reason: {Reason}, Initiator: {Initiator}")]
    private partial void LogConnectionShutdownDetected(string reason, object initiator);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Information,
        Message = "Starting RabbitMQ connection retry timer with interval: {Interval}")]
    private partial void LogStartingConnectionRetryTimer(TimeSpan interval);

    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Debug,
        Message = "RabbitMQ connection is healthy, stopping retry timer")]
    private partial void LogConnectionIsHealthy();

    [LoggerMessage(
        EventId = 9,
        Level = LogLevel.Information,
        Message = "Attempting to reconnect to RabbitMQ...")]
    private partial void LogAttemptingToReconnect();

    [LoggerMessage(
        EventId = 10,
        Level = LogLevel.Information,
        Message = "RabbitMQ reconnection successful")]
    private partial void LogReconnectionSuccessful();

    [LoggerMessage(
        EventId = 11,
        Level = LogLevel.Warning,
        Message = "RabbitMQ reconnection failed, will retry in {Interval}")]
    private partial void LogReconnectionFailed(TimeSpan interval);

    [LoggerMessage(
        EventId = 12,
        Level = LogLevel.Error,
        Message = "Error during RabbitMQ reconnection attempt: {ErrorMessage}")]
    private partial void LogErrorDuringReconnectionAttempt(string errorMessage, Exception ex);

    #endregion
}

#if NETSTANDARD2_0

internal partial class RabbitMqChannelManager
{
    private partial void LogRetryingConnection(int retry, int retryCount, Exception ex)
        => _logger.LogInformation(ex, "Retrying {Retry} of {RetryCount} connection to RabbitMQ...", retry, retryCount);

    private partial void LogConnectionEstablishedSuccessfully()
        => _logger.LogInformation("RabbitMQ connection established successfully");

    private partial void LogCouldNotInitializeConnection(int retryCount, string errorMessage, Exception ex)
        => _logger.LogError(ex, "Could not initialize RabbitMQ connection after {RetryCount} attempts: {ErrorMessage}", retryCount, errorMessage);

    private partial void LogChannelNotAvailableAttemptingReconnection()
        => _logger.LogWarning("Channel is not available, attempting immediate reconnection");

    private partial void LogFailedToEstablishConnection(string errorMessage, Exception ex)
        => _logger.LogWarning(ex, "Failed to establish RabbitMQ connection: {ErrorMessage}", errorMessage);

    private partial void LogFailedToProvisionTopology(string errorMessage, Exception ex)
        => _logger.LogError(ex, "Failed to provision RabbitMQ topology: {ErrorMessage}", errorMessage);

    private partial void LogConnectionShutdownDetected(string reason, object initiator)
        => _logger.LogWarning("RabbitMQ connection shutdown detected. Reason: {Reason}, Initiator: {Initiator}", reason, initiator);

    private partial void LogStartingConnectionRetryTimer(TimeSpan interval)
        => _logger.LogInformation("Starting RabbitMQ connection retry timer with interval: {Interval}", interval);

    private partial void LogConnectionIsHealthy()
        => _logger.LogDebug("RabbitMQ connection is healthy, stopping retry timer");

    private partial void LogAttemptingToReconnect()
        => _logger.LogInformation("Attempting to reconnect to RabbitMQ...");

    private partial void LogReconnectionSuccessful()
        => _logger.LogInformation("RabbitMQ reconnection successful");

    private partial void LogReconnectionFailed(TimeSpan interval)
        => _logger.LogWarning("RabbitMQ reconnection failed, will retry in {Interval}", interval);

    private partial void LogErrorDuringReconnectionAttempt(string errorMessage, Exception ex)
        => _logger.LogError(ex, "Error during RabbitMQ reconnection attempt: {ErrorMessage}", errorMessage);
}

#endif