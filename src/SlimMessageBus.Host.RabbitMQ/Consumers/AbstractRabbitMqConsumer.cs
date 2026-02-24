namespace SlimMessageBus.Host.RabbitMQ;

using global::RabbitMQ.Client;
using global::RabbitMQ.Client.Events;

public abstract partial class AbstractRabbitMqConsumer : AbstractConsumer
{
    private readonly IRabbitMqChannel _channel;
    private readonly IHeaderValueConverter _headerValueConverter;
    private AsyncEventingBasicConsumer _consumer;
    private string _consumerTag;
    private readonly object _consumerLock = new();
    private volatile bool _transportStarted;
    private readonly ILogger _logger;

    protected abstract RabbitMqMessageAcknowledgementMode AcknowledgementMode { get; }

    protected AbstractRabbitMqConsumer(ILogger logger,
                                       IEnumerable<AbstractConsumerSettings> consumerSettings,
                                       IEnumerable<IAbstractConsumerInterceptor> interceptors,
                                       IRabbitMqChannel channel,
                                       string queueName,
                                       IHeaderValueConverter headerValueConverter)
        : base(logger,
               consumerSettings,
               path: queueName,
               interceptors)
    {
        _logger = logger;
        _channel = channel;
        _headerValueConverter = headerValueConverter;

        // Subscribe to channel recovery events
        if (_channel is RabbitMqChannelManager channelManager)
        {
            channelManager.ChannelRecovered += OnChannelRecovered;
        }
    }

    private async void OnChannelRecovered(object sender, EventArgs e)
    {
        if (!_transportStarted)
        {
            // Consumer was deliberately stopped (e.g. by a circuit breaker). Skip re-registration
            // and let the circuit breaker resume the consumer when it is appropriate.
            LogChannelRecoveredButPaused(Path);
            return;
        }

        LogChannelRecovered(Path);

        try
        {
            // Re-register the consumer
            await ReRegisterConsumer();
        }
        catch (Exception ex)
        {
            LogChannelRecoveryFailed(Path, ex.Message, ex);
        }
    }

    protected override Task OnStart()
    {
        _transportStarted = true;
        return ReRegisterConsumer();
    }

    private Task ReRegisterConsumer()
    {
        lock (_consumerLock)
        {
            // Cancel existing consumer if any
            if (_consumerTag != null && _channel.Channel != null && _channel.Channel.IsOpen)
            {
                try
                {
                    lock (_channel.ChannelLock)
                    {
                        _channel.Channel.BasicCancel(_consumerTag);
                    }
                }
                catch (Exception ex)
                {
                    LogCancelExistingConsumerFailed(_consumerTag, Path, ex);
                }
            }

            // Create new consumer
            _consumer = new AsyncEventingBasicConsumer(_channel.Channel);
            _consumer.Received += OnMessageReceived;

            lock (_channel.ChannelLock)
            {
                _consumerTag = _channel.Channel.BasicConsume(Path, autoAck: AcknowledgementMode == RabbitMqMessageAcknowledgementMode.AckAutomaticByRabbit, _consumer);
            }

            LogConsumerRegistered(Path, _consumerTag);
        }

        return Task.CompletedTask;
    }

    protected override Task OnStop()
    {
        lock (_consumerLock)
        {
            _transportStarted = false;

            if (_consumerTag != null && _channel.Channel != null && _channel.Channel.IsOpen)
            {
                try
                {
                    lock (_channel.ChannelLock)
                    {
                        _channel.Channel.BasicCancel(_consumerTag);
                    }
                }
                catch (Exception ex)
                {
                    LogCancelConsumerFailed(_consumerTag, Path, ex);
                }
            }
            _consumerTag = null;

            if (_consumer != null)
            {
                _consumer.Received -= OnMessageReceived;
                _consumer = null;
            }
        }

        return Task.CompletedTask;
    }

    protected async Task OnMessageReceived(object sender, BasicDeliverEventArgs @event)
    {
        if (_consumer == null)
        {
            // In case during shutdown some outstanding message is delivered
            return;
        }

        LogMessageArrived(Path, @event.Exchange, @event.DeliveryTag);
        Exception exception;
        try
        {
            var messageHeaders = new Dictionary<string, object>();

            if (@event.BasicProperties.Headers != null)
            {
                foreach (var header in @event.BasicProperties.Headers)
                {
                    messageHeaders.Add(header.Key, _headerValueConverter.ConvertFrom(header.Value));
                }
            }

            exception = await OnMessageReceived(messageHeaders, @event);
        }
        catch (Exception ex)
        {
            exception = ex;
        }
        if (exception != null)
        {
            LogMessageProcessingError(Path, @event.Exchange, exception.Message, exception);
        }
    }

    protected abstract Task<Exception> OnMessageReceived(Dictionary<string, object> messageHeaders, BasicDeliverEventArgs transportMessage);

    public void NackMessage(BasicDeliverEventArgs @event, bool requeue)
    {
        lock (_channel.ChannelLock)
        {
            // ToDo: Introduce a setting for allowing the client to allow for batching acks
            _channel.Channel.BasicNack(@event.DeliveryTag, multiple: false, requeue: requeue);
        }
    }

    public void AckMessage(BasicDeliverEventArgs @event)
    {
        lock (_channel.ChannelLock)
        {
            // ToDo: Introduce a setting for allowing the client to allow for batching acks
            _channel.Channel.BasicAck(@event.DeliveryTag, multiple: false);
        }
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        // Unsubscribe from channel recovery events
        if (_channel is RabbitMqChannelManager channelManager)
        {
            channelManager.ChannelRecovered -= OnChannelRecovered;
        }

        await base.DisposeAsyncCore();
    }

    #region Logging

#if NETSTANDARD2_0

    private void LogChannelRecoveredButPaused(string queueName)
        => _logger.LogDebug("Channel recovered but consumer for queue {QueueName} is paused - skipping re-registration", queueName);

    private void LogChannelRecovered(string queueName)
        => _logger.LogInformation("Channel recovered, re-registering consumer for queue {QueueName}", queueName);

    private void LogChannelRecoveryFailed(string queueName, string errorMessage, Exception ex)
       => _logger.LogError(ex, "Failed to re-register consumer for queue {QueueName} after channel recovery: {ErrorMessage}", queueName, errorMessage);

    private void LogCancelExistingConsumerFailed(string consumerTag, string queueName, Exception ex)
        => _logger.LogWarning(ex, "Failed to cancel existing consumer tag {ConsumerTag} for queue {QueueName}", consumerTag, queueName);

    private void LogConsumerRegistered(string queueName, string consumerTag)
        => _logger.LogDebug("Consumer registered for queue {QueueName} with tag {ConsumerTag}", queueName, consumerTag);

    private void LogCancelConsumerFailed(string consumerTag, string queueName, Exception ex)
        => _logger.LogWarning(ex, "Failed to cancel consumer tag {ConsumerTag} for queue {QueueName} during stop", consumerTag, queueName);

    private void LogMessageArrived(string queueName, string exchangeName, ulong deliveryTag)
       => _logger.LogDebug("Message arrived on queue {QueueName} from exchange {ExchangeName} with delivery tag {DeliveryTag}", queueName, exchangeName, deliveryTag);

    private void LogMessageProcessingError(string queueName, string exchangeName, string errorMessage, Exception ex)
        => _logger.LogError(ex, "Error while processing message on queue {QueueName} from exchange {ExchangeName}: {ErrorMessage}", queueName, exchangeName, errorMessage);

#else

    [LoggerMessage(
       EventId = 0,
       Level = LogLevel.Debug,
       Message = "Channel recovered but consumer for queue {QueueName} is paused - skipping re-registration")]
    private partial void LogChannelRecoveredButPaused(string queueName);

    [LoggerMessage(
       EventId = 1,
       Level = LogLevel.Information,
       Message = "Channel recovered, re-registering consumer for queue {QueueName}")]
    private partial void LogChannelRecovered(string queueName);

    [LoggerMessage(
       EventId = 2,
       Level = LogLevel.Error,
       Message = "Failed to re-register consumer for queue {QueueName} after channel recovery: {ErrorMessage}")]
    private partial void LogChannelRecoveryFailed(string queueName, string errorMessage, Exception ex);

    [LoggerMessage(
       EventId = 3,
       Level = LogLevel.Warning,
       Message = "Failed to cancel existing consumer tag {ConsumerTag} for queue {QueueName}")]
    private partial void LogCancelExistingConsumerFailed(string consumerTag, string queueName, Exception ex);

    [LoggerMessage(
       EventId = 4,
       Level = LogLevel.Debug,
       Message = "Consumer registered for queue {QueueName} with tag {ConsumerTag}")]
    private partial void LogConsumerRegistered(string queueName, string consumerTag);

    [LoggerMessage(
       EventId = 5,
       Level = LogLevel.Warning,
       Message = "Failed to cancel consumer tag {ConsumerTag} for queue {QueueName} during stop")]
    private partial void LogCancelConsumerFailed(string consumerTag, string queueName, Exception ex);

    [LoggerMessage(
       EventId = 6,
       Level = LogLevel.Debug,
       Message = "Message arrived on queue {QueueName} from exchange {ExchangeName} with delivery tag {DeliveryTag}")]
    private partial void LogMessageArrived(string queueName, string exchangeName, ulong deliveryTag);

    [LoggerMessage(
       EventId = 7,
       Level = LogLevel.Error,
       Message = "Error while processing message on queue {QueueName} from exchange {ExchangeName}: {ErrorMessage}")]
    private partial void LogMessageProcessingError(string queueName, string exchangeName, string errorMessage, Exception ex);

#endif

    #endregion
}

