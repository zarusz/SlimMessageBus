namespace SlimMessageBus.Host.RabbitMQ;

using global::RabbitMQ.Client;

using Microsoft.Extensions.Logging;

/// <summary>
/// Manages RabbitMQ channel connection, reconnection, retry logic, and background timer.
/// Acts as an IModel wrapper with automatic connection recovery.
/// </summary>
internal partial class RabbitMqChannelManager : IRabbitMqChannel, IAsyncDisposable, IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly RabbitMqMessageBusSettings _providerSettings;
    private readonly MessageBusSettings _settings;
    private readonly Func<bool> _isDisposingOrDisposed;

    private IConnection _connection;
    private IChannel _channel;
    private IChannel _confirmsChannel;

    // Use SemaphoreSlim for async-compatible locking
    private readonly SemaphoreSlim _channelLock = new(1, 1);

    // Use SemaphoreSlim to ensure only one connection attempt at a time
    private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);

    private readonly Timer _connectionRetryTimer;
    private readonly TimeSpan _connectionRetryInterval;
    private int _disposed; // 0 = not disposed, 1 = disposed
    private bool? _needsConfirmsChannel; // Cached result of NeedsConfirmsChannel()

    /// <summary>
    /// Event raised when the channel has been successfully recovered after a connection loss.
    /// Consumers should subscribe to this event to re-register themselves with the new channel.
    /// </summary>
    public event EventHandler<EventArgs> ChannelRecovered;

    /// <summary>
    /// Gets the current channel. IChannel is thread-safe in v7+ and can be used concurrently.
    /// </summary>
    public IChannel Channel => _channel;

    /// <summary>
    /// Gets the channel with publisher confirms enabled, or <c>null</c> if no producer requires confirms.
    /// </summary>
    public IChannel ConfirmsChannel => _confirmsChannel;

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
                operation: async (ct) =>
                {
                    connectionEstablished = await EstablishConnection();
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
    /// Returns a snapshot of both channels to avoid TOCTOU races between check and use.
    /// </summary>
    public ChannelSnapshot EnsureChannel()
    {
        var channel = _channel;
        var confirmsChannel = _confirmsChannel;

        if (channel == null)
        {
            // If channel is null, trigger immediate reconnection attempt
            // The semaphore in EstablishConnection will prevent concurrent attempts
            LogChannelNotAvailableAttemptingReconnection();
            OnConnectionRetryTimer(null);

            throw new ProducerMessageBusException("The Channel is not available at this time");
        }

        return new ChannelSnapshot(channel, confirmsChannel);
    }

    private async Task<bool> EstablishConnection()
    {
        // Ensure only one connection attempt at a time using semaphore
        if (!await _connectionSemaphore.WaitAsync(0))
        {
            // Another connection attempt is in progress
            LogConnectionAttemptAlreadyInProgress();
            return false;
        }

        try
        {
            // See https://www.rabbitmq.com/client-libraries/dotnet-api-guide#connection-recovery
            _providerSettings.ConnectionFactory.AutomaticRecoveryEnabled = true;
            // DispatchConsumersAsync is removed in 7.x - consumers are always async

            // Set up connection recovery event handlers
            var newConnection = _providerSettings.Endpoints != null && _providerSettings.Endpoints.Count > 0
                ? await _providerSettings.ConnectionFactory.CreateConnectionAsync(_providerSettings.Endpoints)
                : await _providerSettings.ConnectionFactory.CreateConnectionAsync();

            // Subscribe to connection shutdown events for better resilience
            newConnection.ConnectionShutdownAsync += OnConnectionShutdownAsync;

            var needsConfirmsChannel = NeedsConfirmsChannel();

            IConnection oldConnection = null;
            IChannel oldChannel = null;
            IChannel oldConfirmsChannel = null;
            var isRecovery = false;

            // Use semaphore instead of lock for async safety
            await _channelLock.WaitAsync();
            try
            {
                // Clean up existing connection
                if (_connection != null)
                {
                    _connection.ConnectionShutdownAsync -= OnConnectionShutdownAsync;
                    isRecovery = true;

                    // Store old resources for cleanup outside lock
                    oldConnection = _connection;
                    oldChannel = _channel;
                    oldConfirmsChannel = _confirmsChannel;
                }

                _connection = newConnection;
                _channel = null; // Will be set after creation
                _confirmsChannel = null;
            }
            finally
            {
                _channelLock.Release();
            }

            // Cleanup old resources outside the lock
            if (oldConfirmsChannel != null)
            {
                await CloseAndDisposeChannelAsync(oldConfirmsChannel);
            }
            if (oldChannel != null)
            {
                await CloseAndDisposeChannelAsync(oldChannel);
            }
            if (oldConnection != null)
            {
                await CloseAndDisposeConnectionAsync(oldConnection);
            }

            // Create channels outside the lock to avoid holding lock during async operations
            if (_connection != null)
            {
                var newChannel = await _connection.CreateChannelAsync();

                IChannel newConfirmsChannel = null;
                if (needsConfirmsChannel)
                {
                    newConfirmsChannel = await _connection.CreateChannelAsync(
                        new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true));
                }

                await _channelLock.WaitAsync();
                try
                {
                    _channel = newChannel;
                    _confirmsChannel = newConfirmsChannel;
                }
                finally
                {
                    _channelLock.Release();
                }

                // Provision topology (uses the main channel)
                await ProvisionTopology();
            }

            // Notify consumers to re-register after recovery
            if (isRecovery)
            {
                LogChannelRecoveredNotifyingConsumers();
                ChannelRecovered?.Invoke(this, EventArgs.Empty);
            }

            return true;
        }
        catch (Exception ex)
        {
            LogFailedToEstablishConnection(ex.Message, ex);
            return false;
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    /// <summary>
    /// Determines if a separate confirms channel is needed based on bus-level and producer-level settings.
    /// The result is cached after the first call since producer configuration does not change at runtime.
    /// </summary>
    internal bool NeedsConfirmsChannel()
    {
        _needsConfirmsChannel ??= ComputeNeedsConfirmsChannel();
        return _needsConfirmsChannel.Value;
    }

    private bool ComputeNeedsConfirmsChannel()
    {
        // If bus-level confirms are enabled, we always need a confirms channel
        // (some producers might opt out and use the regular channel)
        if (_providerSettings.EnablePublisherConfirms)
        {
            return true;
        }

        // Check if any producer explicitly enables confirms
        return _settings.Producers.Any(p => p.GetOrDefault<bool?>(RabbitMqProperties.EnablePublisherConfirms, null) == true);
    }

    private async Task ProvisionTopology()
    {
        if (_channel == null) return;

        try
        {
            var topologyService = new RabbitMqTopologyService(_loggerFactory, _channel, _settings, _providerSettings);

            var customAction = _providerSettings.GetOrDefault(RabbitMqProperties.TopologyInitializer);
            if (customAction != null)
            {
                // Allow the user to specify its own initializer
                await customAction(_channel, topologyService.ProvisionTopology);
            }
            else
            {
                // Perform default topology setup
                await topologyService.ProvisionTopology();
            }
        }
        catch (Exception ex)
        {
            LogFailedToProvisionTopology(ex.Message, ex);
            throw;
        }
    }

    private Task OnConnectionShutdownAsync(object sender, ShutdownEventArgs e)
    {
        if (!_isDisposingOrDisposed())
        {
            LogConnectionShutdownDetected(e.ReplyText, e.Initiator);

            // Start retry timer if not already running
            StartConnectionRetryTimer();
        }
        return Task.CompletedTask;
    }

    private void StartConnectionRetryTimer()
    {
        if (_isDisposingOrDisposed())
            return;

        // Use one-shot timer (Timeout.Infinite for period) to avoid piling up fire-and-forget tasks
        LogStartingConnectionRetryTimer(_connectionRetryInterval);
        _connectionRetryTimer.Change(_connectionRetryInterval, Timeout.InfiniteTimeSpan);
    }

    private void OnConnectionRetryTimer(object state)
    {
        if (_isDisposingOrDisposed())
            return;

        // Check if connection is already healthy (quick check without lock)
        if (_connection?.IsOpen == true && _channel?.IsOpen == true
            && (_confirmsChannel == null || _confirmsChannel.IsOpen))
        {
            LogConnectionIsHealthy();
            _connectionRetryTimer.Change(Timeout.Infinite, Timeout.Infinite);
            return;
        }

        // Fire and forget - don't block the timer thread
        // The semaphore in EstablishConnection will prevent concurrent attempts
        _ = Task.Run(async () =>
        {
            if (_isDisposingOrDisposed())
                return;

            try
            {
                LogAttemptingToReconnect();

                var success = await EstablishConnection();
                if (success)
                {
                    LogReconnectionSuccessful();
                }
                else
                {
                    LogReconnectionFailed(_connectionRetryInterval);
                    // Re-arm the one-shot timer for the next attempt
                    StartConnectionRetryTimer();
                }
            }
            catch (Exception ex)
            {
                LogErrorDuringReconnectionAttempt(ex.Message, ex);
                // Re-arm the one-shot timer for the next attempt
                StartConnectionRetryTimer();
            }
        });
    }

    /// <summary>
    /// Safely closes and disposes a RabbitMQ channel asynchronously.
    /// </summary>
    private static async Task CloseAndDisposeChannelAsync(IChannel channel)
    {
        if (channel != null)
        {
            try
            {
                if (!channel.IsClosed)
                {
                    await channel.CloseAsync();
                }
            }
            catch
            {
                // Ignore exceptions during cleanup
            }
            finally
            {
                try
                {
                    channel.Dispose();
                }
                catch
                {
                    // Ignore disposal exceptions
                }
            }
        }
    }

    /// <summary>
    /// Safely closes and disposes a RabbitMQ connection asynchronously.
    /// </summary>
    private static async Task CloseAndDisposeConnectionAsync(IConnection connection)
    {
        if (connection != null)
        {
            try
            {
                await connection.CloseAsync();
            }
            catch
            {
                // Ignore exceptions during cleanup
            }
            finally
            {
                try
                {
                    connection.Dispose();
                }
                catch
                {
                    // Ignore disposal exceptions
                }
            }
        }
    }

    #region Disposable

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return; // Already disposed

        if (disposing)
        {
            // Stop the retry timer
            _connectionRetryTimer?.Dispose();

            // For synchronous dispose, we have to block (not ideal but necessary for IDisposable)
            DisposeResourcesAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return; // Already disposed

        // Stop the retry timer
        _connectionRetryTimer?.Dispose();

        await DisposeResourcesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Shared cleanup method called by both synchronous and asynchronous dispose paths.
    /// Must only be called once (guarded by the <see cref="_disposed"/> flag in the callers).
    /// </summary>
    private async ValueTask DisposeResourcesAsync()
    {
        IConnection connectionToDispose = null;
        IChannel channelToDispose = null;
        IChannel confirmsChannelToDispose = null;

        // Acquire lock to safely extract resources
        await _channelLock.WaitAsync().ConfigureAwait(false);
        try
        {
            channelToDispose = _channel;
            confirmsChannelToDispose = _confirmsChannel;
            connectionToDispose = _connection;

            _channel = null;
            _confirmsChannel = null;
            _connection = null;

            // Unsubscribe from events
            if (connectionToDispose != null)
            {
                connectionToDispose.ConnectionShutdownAsync -= OnConnectionShutdownAsync;
            }
        }
        finally
        {
            _channelLock.Release();
        }

        // Dispose resources outside the lock
        if (confirmsChannelToDispose != null)
        {
            await CloseAndDisposeChannelAsync(confirmsChannelToDispose).ConfigureAwait(false);
        }

        if (channelToDispose != null)
        {
            await CloseAndDisposeChannelAsync(channelToDispose).ConfigureAwait(false);
        }

        if (connectionToDispose != null)
        {
            await CloseAndDisposeConnectionAsync(connectionToDispose).ConfigureAwait(false);
        }

        // Dispose semaphores
        _channelLock?.Dispose();
        _connectionSemaphore?.Dispose();
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

    [LoggerMessage(
        EventId = 13,
        Level = LogLevel.Information,
        Message = "RabbitMQ channel recovered, notifying consumers to re-register")]
    private partial void LogChannelRecoveredNotifyingConsumers();

    [LoggerMessage(
        EventId = 14,
        Level = LogLevel.Debug,
        Message = "RabbitMQ connection attempt already in progress, skipping")]
    private partial void LogConnectionAttemptAlreadyInProgress();

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

    private partial void LogChannelRecoveredNotifyingConsumers()
        => _logger.LogInformation("RabbitMQ channel recovered, notifying consumers to re-register");

    private partial void LogConnectionAttemptAlreadyInProgress()
        => _logger.LogDebug("RabbitMQ connection attempt already in progress, skipping");
}

#endif