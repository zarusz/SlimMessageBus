namespace SlimMessageBus.Host.Relational;

using System.Diagnostics;

using Microsoft.Extensions.DependencyInjection;

using SlimMessageBus.Host.Consumer;
using SlimMessageBus.Host.Interceptor;

public abstract class RelationalConsumerBase<TMessage, TRepository> : AbstractConsumer
    where TMessage : IRelationalTransportMessage
    where TRepository : IRelationalRepository<TMessage>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IRelationalMessageBusSettings _providerSettings;
    private readonly IMessageProcessor<TMessage> _messageProcessor;
    private readonly PathKind _pathKind;
    private readonly string _subscriptionName;
    private readonly string _instanceId;
    private readonly string _transportName;
    private Task _task;

    protected RelationalConsumerBase(
        ILogger logger,
        IEnumerable<AbstractConsumerSettings> consumerSettings,
        IEnumerable<IAbstractConsumerInterceptor> interceptors,
        IServiceProvider serviceProvider,
        IRelationalMessageBusSettings providerSettings,
        IMessageProcessor<TMessage> messageProcessor,
        string path,
        PathKind pathKind,
        string subscriptionName,
        string instanceId,
        string transportName)
        : base(logger, consumerSettings, path, interceptors)
    {
        _serviceProvider = serviceProvider;
        _providerSettings = providerSettings;
        _messageProcessor = messageProcessor;
        _pathKind = pathKind;
        _subscriptionName = subscriptionName;
        _instanceId = instanceId;
        _transportName = transportName;
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
            catch (OperationCanceledException e) when (CancellationToken.IsCancellationRequested)
            {
                Logger.LogDebug(e, "{TransportName} consumer for path {Path} stopped", _transportName, Path);
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
                Logger.LogWarning(e, "Error occurred while polling {TransportName} path {Path}", _transportName, Path);
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
            var repository = scope.ServiceProvider.GetRequiredService<TRepository>();
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
            if (scope is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                scope.Dispose();
            }
        }
    }

    private async Task DelayWhenIdle(Stopwatch idle)
    {
        if (idle.Elapsed >= _providerSettings.PollDelay)
        {
            await Task.Delay(_providerSettings.PollDelay, CancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessMessages(TRepository repository, IReadOnlyCollection<TMessage> messages)
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
                Logger.LogError(result.Exception, "Error occurred while processing {TransportName} message {MessageId} on {Path}", _transportName, message.Id, Path);
                await repository.Fail([message], _providerSettings.MaxDeliveryAttempts, CancellationToken).ConfigureAwait(false);
            }
        }
    }
}
