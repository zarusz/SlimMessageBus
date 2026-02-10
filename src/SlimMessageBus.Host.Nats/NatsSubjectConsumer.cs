#nullable enable
namespace SlimMessageBus.Host.Nats;

public class NatsSubjectConsumer<TType> : AbstractConsumer
{
    private readonly INatsConnection _connection;
    private readonly IMessageProcessor<NatsMsg<TType>> _messageProcessor;
    private readonly string _queueGroup;
    private INatsSub<TType>? _subscription;
    private Task? _messageConsumerTask;

    public NatsSubjectConsumer(ILogger logger,
                               IEnumerable<AbstractConsumerSettings> consumerSettings,
                               IEnumerable<IAbstractConsumerInterceptor> interceptors,
                               string subject,
                               string queueGroup,
                               INatsConnection connection,
                               IMessageProcessor<NatsMsg<TType>> messageProcessor)
        : base(logger,
               consumerSettings,
               path: subject,
               interceptors)
    {
        _connection = connection;
        _messageProcessor = messageProcessor;
        _queueGroup = queueGroup;
    }

    protected override async Task OnStart()
    {
        _subscription ??= await _connection.SubscribeCoreAsync<TType>(Path, queueGroup: _queueGroup, cancellationToken: CancellationToken);

        _messageConsumerTask = Task.Factory.StartNew(OnLoop, CancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
    }

    protected override async Task OnStop()
    {
        if (_messageConsumerTask != null)
        {
            await _messageConsumerTask.ConfigureAwait(false);
        }

        if (_subscription != null)
        {
            await _subscription.UnsubscribeAsync().ConfigureAwait(false);
            await _subscription.DisposeAsync();
        }
    }

    private async Task OnLoop()
    {
        try
        {
            while (await _subscription!.Msgs.WaitToReadAsync(CancellationToken))
            {
                while (_subscription.Msgs.TryRead(out var msg))
                {
                    await _messageProcessor.ProcessMessage(msg, msg.Headers.ToReadOnlyDictionary(), cancellationToken: CancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation("Consumer task was cancelled");
        }
    }
}