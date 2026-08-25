namespace SlimMessageBus.Host.GooglePubSub;

public class GooglePubSubConsumer : AbstractConsumer
{
    private readonly GooglePubSubMessageBus _messageBus;
    private readonly SubscriptionName _subscriptionName;
    private readonly IMessageProcessor<PubsubMessage> _messageProcessor;
    private readonly int _concurrency;
    private SubscriberClient _subscriberClient;
    private SemaphoreSlim _concurrencySemaphore;
    private Task _subscriberTask;

    public GooglePubSubConsumer(
        GooglePubSubMessageBus messageBus,
        SubscriptionName subscriptionName,
        IMessageProcessor<PubsubMessage> messageProcessor,
        IEnumerable<AbstractConsumerSettings> consumerSettings)
        : base(
            messageBus.LoggerFactory.CreateLogger<GooglePubSubConsumer>(),
            consumerSettings,
            subscriptionName.ToString(),
            messageBus.Settings.ServiceProvider.GetServices<IAbstractConsumerInterceptor>())
    {
        _messageBus = messageBus;
        _subscriptionName = subscriptionName;
        _messageProcessor = messageProcessor;

        var concurrencyValues = Settings.Select(x => x.Instances).Distinct().ToList();
        if (concurrencyValues.Count > 1)
        {
            throw new ConfigurationMessageBusException($"All consumers on subscription {_subscriptionName} must use the same Instances setting.");
        }
        _concurrency = concurrencyValues.SingleOrDefault();
        if (_concurrency <= 0)
        {
            _concurrency = 1;
        }
    }

    protected override async Task OnStart()
    {
        _concurrencySemaphore = new SemaphoreSlim(_concurrency, _concurrency);
        _subscriberClient = await _messageBus.ProviderSettings.SubscriberClientFactory(
            _messageBus.Settings.ServiceProvider,
            _subscriptionName,
            CancellationToken).ConfigureAwait(false);

        if (Logger.IsEnabled(LogLevel.Information))
        {
            Logger.LogInformation("Starting Google Pub/Sub consumer for subscription {Subscription}", _subscriptionName);
        }
        _subscriberTask = _subscriberClient.StartAsync(ProcessMessage);
    }

    private async Task<SubscriberClient.Reply> ProcessMessage(PubsubMessage message, CancellationToken cancellationToken)
    {
        await _concurrencySemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return SubscriberClient.Reply.Nack;
            }

            var headers = _messageBus.DeserializeHeaders(message.Attributes);
            var result = await _messageProcessor.ProcessMessage(message, headers, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.Result is ProcessResult.SuccessState)
            {
                if (Logger.IsEnabled(LogLevel.Debug))
                {
                    Logger.LogDebug("Acknowledging Pub/Sub message {MessageId} on subscription {Subscription}", message.MessageId, _subscriptionName);
                }
                return SubscriberClient.Reply.Ack;
            }

            if (Logger.IsEnabled(LogLevel.Warning))
            {
                Logger.LogWarning(result.Exception, "Negatively acknowledging Pub/Sub message {MessageId} on subscription {Subscription}", message.MessageId, _subscriptionName);
            }
            return SubscriberClient.Reply.Nack;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return SubscriberClient.Reply.Nack;
        }
        finally
        {
            _concurrencySemaphore.Release();
        }
    }

    protected override async Task OnStop()
    {
        if (_subscriberClient == null)
        {
            return;
        }

        if (Logger.IsEnabled(LogLevel.Information))
        {
            Logger.LogInformation("Stopping Google Pub/Sub consumer for subscription {Subscription}", _subscriptionName);
        }
        await _subscriberClient.StopAsync(new SubscriberClient.ShutdownOptions(), CancellationToken.None).ConfigureAwait(false);
        if (_subscriberTask != null)
        {
            await _subscriberTask.ConfigureAwait(false);
            _subscriberTask = null;
        }
        await _subscriberClient.DisposeAsync().ConfigureAwait(false);
        _subscriberClient = null;

        _concurrencySemaphore?.Dispose();
        _concurrencySemaphore = null;
    }
}
