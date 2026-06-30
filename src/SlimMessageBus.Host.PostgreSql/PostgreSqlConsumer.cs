namespace SlimMessageBus.Host.PostgreSql;

using System.Diagnostics;

public class PostgreSqlConsumer : AbstractConsumer
{
    private readonly IServiceProvider _serviceProvider;
    private readonly PostgreSqlMessageBusSettings _providerSettings;
    private readonly IMessageProcessor<PostgreSqlTransportMessage> _messageProcessor;
    private readonly PathKind _pathKind;
    private readonly string? _subscriptionName;
    private readonly string _instanceId;
    private Task? _task;

    public PostgreSqlConsumer(
        ILogger<PostgreSqlConsumer> logger,
        IEnumerable<AbstractConsumerSettings> consumerSettings,
        IEnumerable<IAbstractConsumerInterceptor> interceptors,
        IServiceProvider serviceProvider,
        PostgreSqlMessageBusSettings providerSettings,
        IMessageProcessor<PostgreSqlTransportMessage> messageProcessor,
        string path,
        PathKind pathKind,
        string? subscriptionName,
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
                var scope = _serviceProvider.CreateScope();
                try
                {
                    var repository = scope.ServiceProvider.GetRequiredService<IPostgreSqlRepository>();
                    var messages = await repository.LockAndSelect(Path, _pathKind, _subscriptionName, _instanceId, _providerSettings.PollBatchSize, _providerSettings.LockDuration, CancellationToken).ConfigureAwait(false);

                    if (messages.Count == 0)
                    {
                        if (idle.Elapsed >= _providerSettings.PollDelay)
                        {
                            await Task.Delay(_providerSettings.PollDelay, CancellationToken).ConfigureAwait(false);
                        }
                        continue;
                    }

                    idle.Restart();

                    foreach (var message in messages)
                    {
                        var result = await _messageProcessor.ProcessMessage(message, message.Headers, cancellationToken: CancellationToken).ConfigureAwait(false);
                        if (result.Exception == null)
                        {
                            await repository.Complete([message], CancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            Logger.LogError(result.Exception, "Error occurred while processing PostgreSQL message {MessageId} on {Path}", message.Id, Path);
                            await repository.Fail([message], _providerSettings.MaxDeliveryAttempts, CancellationToken).ConfigureAwait(false);
                        }
                    }
                }
                finally
                {
                    await scope.DisposeAsyncScope().ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e) when (!CancellationToken.IsCancellationRequested)
            {
                Logger.LogWarning(e, "Error occurred while polling PostgreSQL path {Path}", Path);
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
}
