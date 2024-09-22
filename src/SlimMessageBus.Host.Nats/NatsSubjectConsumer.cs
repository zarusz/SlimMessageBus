#nullable enable
namespace SlimMessageBus.Host.Nats;

public class NatsSubjectConsumer<TType>(ILogger logger, string subject, INatsConnection connection, IMessageProcessor<NatsMsg<TType>> messageProcessor) : AbstractConsumer(logger)
{
    private INatsSub<TType>? _subscription;
    private Task? _messageConsumerTask;

    protected override async Task OnStart()
    {
        _subscription ??= await connection.SubscribeCoreAsync<TType>(subject, cancellationToken: CancellationToken);

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
                    await messageProcessor.ProcessMessage(msg, msg.Headers.ToReadOnlyDictionary(), cancellationToken: CancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException ex)
        {
            Logger.LogInformation(ex, "Consumer task was cancelled");
        }
    }
}