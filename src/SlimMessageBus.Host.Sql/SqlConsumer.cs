namespace SlimMessageBus.Host.Sql;

using System.Diagnostics;

public class SqlConsumer : AbstractConsumer
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SqlMessageBusSettings _providerSettings;
    private readonly IMessageProcessor<SqlTransportMessage> _messageProcessor;
    private readonly PathKind _pathKind;
    private readonly string _subscriptionName;
    private readonly string _instanceId;
    private Task _task;

    public SqlConsumer(
        ILogger<SqlConsumer> logger,
        IEnumerable<AbstractConsumerSettings> consumerSettings,
        IEnumerable<IAbstractConsumerInterceptor> interceptors,
        IServiceProvider serviceProvider,
        SqlMessageBusSettings providerSettings,
        IMessageProcessor<SqlTransportMessage> messageProcessor,
        string path,
        PathKind pathKind,
        string subscriptionName,
        string instanceId)
        : base(logger, consumerSettings, path, interceptors)
    {
        _serviceProvider = serviceProvider;
        _providerSettings = providerSettings;
        _messageProcessor = messageProcessor;
        _pathKind = pathKind;
        _subscriptionName = subscriptionName;
        _instanceId = instanceId;
    }

    protected override Task OnStart()
    {
        _task = Run();
        return Task.CompletedTask;
    }

    protected override async Task OnStop()
    {
        if (_task != null)
        {
            try
            {
                await _task.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
            {
                Logger.LogDebug("SQL consumer for path {Path} stopped", Path);
            }
            _task = null;
        }
    }

    private async Task Run()
    {
        var idle = Stopwatch.StartNew();

        while (!CancellationToken.IsCancellationRequested)
        {
            try
            {
                await PollOnce(idle).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e) when (!CancellationToken.IsCancellationRequested)
            {
                Logger.LogWarning(e, "Error occurred while polling SQL path {Path}", Path);
                try
                {
                    await Task.Delay(_providerSettings.PollDelay, CancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }

    private async Task PollOnce(Stopwatch idle)
    {
        var scope = _serviceProvider.CreateScope();
        try
        {
            var repository = scope.ServiceProvider.GetRequiredService<ISqlRepository>();
            var messages = await repository.LockAndSelect(Path, _pathKind, _subscriptionName, _instanceId, _providerSettings.PollBatchSize, _providerSettings.LockDuration, CancellationToken).ConfigureAwait(false);

            if (messages.Count == 0)
            {
                await DelayWhenIdle(idle).ConfigureAwait(false);
                return;
            }

            idle.Restart();
            await ProcessMessages(repository, messages).ConfigureAwait(false);
        }
        finally
        {
            await scope.DisposeAsyncScope().ConfigureAwait(false);
        }
    }

    private async Task DelayWhenIdle(Stopwatch idle)
    {
        if (idle.Elapsed >= _providerSettings.PollDelay)
        {
            await Task.Delay(_providerSettings.PollDelay, CancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessMessages(ISqlRepository repository, IReadOnlyCollection<SqlTransportMessage> messages)
    {
        foreach (var message in messages)
        {
            var result = await _messageProcessor.ProcessMessage(message, message.Headers, cancellationToken: CancellationToken).ConfigureAwait(false);
            if (result.Exception == null)
            {
                await repository.Complete([message], CancellationToken).ConfigureAwait(false);
            }
            else
            {
                Logger.LogError(result.Exception, "Error occurred while processing SQL message {MessageId} on {Path}", message.Id, Path);
                await repository.Fail([message], _providerSettings.MaxDeliveryAttempts, CancellationToken).ConfigureAwait(false);
            }
        }
    }
}
