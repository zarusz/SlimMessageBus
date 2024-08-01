namespace SlimMessageBus.Host.Nats;

public class NatsSubjectConsumer<TType>(ILogger logger, string subject, INatsConnection connection, IMessageProcessor<NatsMsg<TType>> messageProcessor) : AbstractConsumer(logger)
{
    private CancellationTokenSource _cancellationTokenSource;
    private INatsSub<TType> _subscription;

    protected override async Task OnStart()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        _subscription ??= await connection.SubscribeCoreAsync<TType>(subject, cancellationToken: _cancellationTokenSource.Token);

        _ = Task.Run(async () =>
        {
            while (await _subscription.Msgs.WaitToReadAsync(_cancellationTokenSource.Token))
            {
                while (_subscription.Msgs.TryRead(out var msg))
                {
                    await messageProcessor.ProcessMessage(msg, msg.Headers.ToReadOnlyDictionary(), cancellationToken: _cancellationTokenSource.Token).ConfigureAwait(false);
                }
            }
        }, _cancellationTokenSource.Token);
    }

    protected override async Task OnStop()
    {
        if (_cancellationTokenSource != null)
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
        }

        if (_subscription != null)
        {
            await _subscription.UnsubscribeAsync().ConfigureAwait(false);
            await _subscription.DisposeAsync();
        }
    }
}