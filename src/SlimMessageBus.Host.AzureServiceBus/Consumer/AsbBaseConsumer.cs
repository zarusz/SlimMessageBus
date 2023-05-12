namespace SlimMessageBus.Host.AzureServiceBus.Consumer;

public abstract class AsbBaseConsumer : IAsyncDisposable, IConsumerControl
{
    private readonly ILogger _logger;
    private ServiceBusProcessor _serviceBusProcessor;
    private ServiceBusSessionProcessor _serviceBusSessionProcessor;

    public ServiceBusMessageBus MessageBus { get; }
    protected IMessageProcessor<ServiceBusReceivedMessage> MessageProcessor { get; }
    protected TopicSubscriptionParams TopicSubscription { get; }

    public bool IsStarted { get; private set; }

    protected AsbBaseConsumer(ServiceBusMessageBus messageBus, ServiceBusClient serviceBusClient, TopicSubscriptionParams subscriptionFactoryParams, IMessageProcessor<ServiceBusReceivedMessage> messageProcessor, IEnumerable<AbstractConsumerSettings> consumerSettings, ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        MessageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
        TopicSubscription = subscriptionFactoryParams ?? throw new ArgumentNullException(nameof(subscriptionFactoryParams));
        MessageProcessor = messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));

        T GetSingleValue<T>(Func<AbstractConsumerSettings, T> selector, string settingName)
        {
            var set = consumerSettings.Select(x => selector(x)).ToHashSet();
            if (set.Count > 1)
            {
                throw new ConfigurationMessageBusException($"All declared consumers across the same path/subscription {TopicSubscription} must have the same {settingName} settings.");
            }
            return set.Single();
        }

        var instances = GetSingleValue(x => x.Instances, nameof(ConsumerSettings.Instances));

        var maxAutoLockRenewalDuration = GetSingleValue(x => x.GetMaxAutoLockRenewalDuration(), nameof(ConsumerBuilderExtensions.MaxAutoLockRenewalDuration))
            ?? messageBus.ProviderSettings.MaxAutoLockRenewalDuration;

        var subQueue = GetSingleValue(x => x.GetSubQueue(), nameof(ConsumerBuilderExtensions.SubQueue));

        var prefetchCount = GetSingleValue(x => x.GetPrefetchCount(), nameof(ConsumerBuilderExtensions.PrefetchCount))
            ?? messageBus.ProviderSettings.PrefetchCount;

        var enableSession = GetSingleValue(x => x.GetEnableSession(), nameof(ConsumerBuilderExtensions.EnableSession));

        var sessionIdleTimeout = GetSingleValue(x => x.GetSessionIdleTimeout(), nameof(ConsumerSessionBuilder.SessionIdleTimeout))
            ?? messageBus.ProviderSettings.SessionIdleTimeout;

        var maxConcurrentSessions = GetSingleValue(x => x.GetMaxConcurrentSessions(), nameof(ConsumerSessionBuilder.MaxConcurrentSessions))
            ?? messageBus.ProviderSettings.MaxConcurrentSessions;

        if (enableSession)
        {
            var options = messageBus.ProviderSettings.SessionProcessorOptionsFactory(subscriptionFactoryParams);
            options.AutoCompleteMessages = false;
            options.MaxConcurrentCallsPerSession = instances;

            if (maxAutoLockRenewalDuration != null) options.MaxAutoLockRenewalDuration = maxAutoLockRenewalDuration.Value;
            if (prefetchCount != null) options.PrefetchCount = prefetchCount.Value;
            if (sessionIdleTimeout != null) options.SessionIdleTimeout = sessionIdleTimeout.Value;
            if (maxConcurrentSessions != null) options.MaxConcurrentSessions = maxConcurrentSessions.Value;

            _serviceBusSessionProcessor = messageBus.ProviderSettings.SessionProcessorFactory(subscriptionFactoryParams, options, serviceBusClient);
            _serviceBusSessionProcessor.ProcessMessageAsync += ServiceBusSessionProcessor_ProcessMessageAsync;
            _serviceBusSessionProcessor.ProcessErrorAsync += ServiceBusSessionProcessor_ProcessErrorAsync;
            _serviceBusSessionProcessor.SessionInitializingAsync += ServiceBusSessionProcessor_SessionInitializingAsync;
            _serviceBusSessionProcessor.SessionClosingAsync += ServiceBusSessionProcessor_SessionClosingAsync;
        }
        else
        {
            var options = messageBus.ProviderSettings.ProcessorOptionsFactory(subscriptionFactoryParams);
            options.AutoCompleteMessages = false;
            options.MaxConcurrentCalls = instances;

            if (maxAutoLockRenewalDuration != null) options.MaxAutoLockRenewalDuration = maxAutoLockRenewalDuration.Value;
            if (prefetchCount != null) options.PrefetchCount = prefetchCount.Value;
            if (subQueue != null) options.SubQueue = subQueue.Value;

            _serviceBusProcessor = messageBus.ProviderSettings.ProcessorFactory(subscriptionFactoryParams, options, serviceBusClient);
            _serviceBusProcessor.ProcessMessageAsync += ServiceBusProcessor_ProcessMessagesAsync;
            _serviceBusProcessor.ProcessErrorAsync += ServiceBusProcessor_ProcessErrorAsync;
        }
    }

    public async Task Start()
    {
        if (IsStarted)
        {
            return;
        }

        _logger.LogInformation("Starting consumer for Path: {Path}, SubscriptionName: {SubscriptionName}", TopicSubscription.Path, TopicSubscription.SubscriptionName);

        if (_serviceBusProcessor != null)
        {
            await _serviceBusProcessor.StartProcessingAsync().ConfigureAwait(false);
        }

        if (_serviceBusSessionProcessor != null)
        {
            await _serviceBusSessionProcessor.StartProcessingAsync().ConfigureAwait(false);
        }

        IsStarted = true;
    }

    public async Task Stop()
    {
        if (!IsStarted)
        {
            return;
        }

        _logger.LogInformation("Stopping consumer for Path: {Path}, SubscriptionName: {SubscriptionName}", TopicSubscription.Path, TopicSubscription.SubscriptionName);
        if (_serviceBusProcessor != null)
        {
            await _serviceBusProcessor.StopProcessingAsync().ConfigureAwait(false);
        }

        if (_serviceBusSessionProcessor != null)
        {
            await _serviceBusSessionProcessor.StopProcessingAsync().ConfigureAwait(false);
        }

        IsStarted = false;
    }

    #region IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (_serviceBusProcessor != null)
        {
            await _serviceBusProcessor.CloseAsync().ConfigureAwait(false);
            _serviceBusProcessor = null;
        }

        if (_serviceBusSessionProcessor != null)
        {
            await _serviceBusSessionProcessor.CloseAsync().ConfigureAwait(false);
            _serviceBusSessionProcessor = null;
        }

        await MessageProcessor.DisposeSilently().ConfigureAwait(false);
    }

    #endregion

    private Task ServiceBusSessionProcessor_SessionInitializingAsync(ProcessSessionEventArgs args)
    {
        _logger.LogDebug("Session with id {SessionId} initializing", args.SessionId);
        return Task.CompletedTask;
    }

    private Task ServiceBusSessionProcessor_SessionClosingAsync(ProcessSessionEventArgs args)
    {
        _logger.LogDebug("Session with id {SessionId} closing", args.SessionId);
        return Task.CompletedTask;
    }

    private Task ServiceBusSessionProcessor_ProcessMessageAsync(ProcessSessionMessageEventArgs args)
        => ProcessMessageAsyncInternal(args.Message, args.CompleteMessageAsync, args.AbandonMessageAsync, args.CancellationToken);

    private Task ServiceBusSessionProcessor_ProcessErrorAsync(ProcessErrorEventArgs args)
        => ProcessErrorAsyncInternal(args.Exception, args.ErrorSource);

    protected Task ServiceBusProcessor_ProcessMessagesAsync(ProcessMessageEventArgs args)
        => ProcessMessageAsyncInternal(args.Message, args.CompleteMessageAsync, args.AbandonMessageAsync, args.CancellationToken);

    protected Task ServiceBusProcessor_ProcessErrorAsync(ProcessErrorEventArgs args)
        => ProcessErrorAsyncInternal(args.Exception, args.ErrorSource);

    protected async Task ProcessMessageAsyncInternal(ServiceBusReceivedMessage message, Func<ServiceBusReceivedMessage, CancellationToken, Task> completeMessage, Func<ServiceBusReceivedMessage, IDictionary<string, object>, CancellationToken, Task> abandonMessage, CancellationToken token)
    {
        // Process the message.
        _logger.LogDebug("Received message - Path: {Path}, SubscriptionName: {SubscriptionName}, SequenceNumber: {SequenceNumber}, DeliveryCount: {DeliveryCount}, MessageId: {MessageId}", TopicSubscription.Path, TopicSubscription.SubscriptionName, message.SequenceNumber, message.DeliveryCount, message.MessageId);

        if (token.IsCancellationRequested)
        {
            // Note: Use the cancellationToken passed as necessary to determine if the subscriptionClient has already been closed.
            // If subscriptionClient has already been closed, you can choose to not call CompleteAsync() or AbandonAsync() etc.
            // to avoid unnecessary exceptions.
            _logger.LogDebug("Abandon message - Path: {Path}, SubscriptionName: {SubscriptionName}, SequenceNumber: {SequenceNumber}, DeliveryCount: {DeliveryCount}, MessageId: {MessageId}", TopicSubscription.Path, TopicSubscription.SubscriptionName, message.SequenceNumber, message.DeliveryCount, message.MessageId);
            await abandonMessage(message, null, token).ConfigureAwait(false);

            return;
        }

        var (exception, _, _, _) = await MessageProcessor.ProcessMessage(message, message.ApplicationProperties, token).ConfigureAwait(false);
        if (exception != null)
        {
            _logger.LogError(exception, "Abandon message (exception occured while processing) - Path: {Path}, SubscriptionName: {SubscriptionName}, SequenceNumber: {SequenceNumber}, DeliveryCount: {DeliveryCount}, MessageId: {MessageId}", TopicSubscription.Path, TopicSubscription.SubscriptionName, message.SequenceNumber, message.DeliveryCount, message.MessageId);

            var messageProperties = new Dictionary<string, object>
            {
                // Set the exception message
                ["SMB.Exception"] = exception.Message
            };
            await abandonMessage(message, messageProperties, token).ConfigureAwait(false);

            return;
        }

        // Complete the message so that it is not received again.
        // This can be done only if the subscriptionClient is created in ReceiveMode.PeekLock mode (which is the default).
        _logger.LogDebug("Complete message - Path: {Path}, SubscriptionName: {SubscriptionName}, SequenceNumber: {SequenceNumber}, DeliveryCount: {DeliveryCount}, MessageId: {MessageId}", TopicSubscription.Path, TopicSubscription.SubscriptionName, message.SequenceNumber, message.DeliveryCount, message.MessageId);
        await completeMessage(message, token).ConfigureAwait(false);
    }

    protected Task ProcessErrorAsyncInternal(Exception exception, ServiceBusErrorSource errorSource)
    {
        _logger.LogError(exception, "Error while processing Path: {Path}, SubscriptionName: {SubscriptionName}, Error Message: {ErrorMessage}, Error Source: {ErrorSource}", TopicSubscription.Path, TopicSubscription.SubscriptionName, exception.Message, errorSource);
        return Task.CompletedTask;
    }
}