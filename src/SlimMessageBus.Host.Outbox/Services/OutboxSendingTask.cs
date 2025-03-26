namespace SlimMessageBus.Host.Outbox.Services;

internal class OutboxSendingTask<TOutboxMessage>(
    ILoggerFactory loggerFactory,
    OutboxSettings outboxSettings,
    IServiceProvider serviceProvider)
    : IMessageBusLifecycleInterceptor, IOutboxNotificationService, IAsyncDisposable
    where TOutboxMessage : OutboxMessage
{
    private readonly ILogger<OutboxSendingTask<TOutboxMessage>> _logger = loggerFactory.CreateLogger<OutboxSendingTask<TOutboxMessage>>();
    private readonly OutboxSettings _outboxSettings = outboxSettings;
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    private readonly AsyncManualResetEvent _asyncManualResetEvent = new();
    private CancellationTokenSource _loopCts;
    private Task _loopTask;

    private readonly object _migrateSchemaTaskLock = new();
    private Task _migrateSchemaTask;
    private Task _startBusTask;
    private Task _stopBusTask;

    private int _busStartCount;

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    public void Notify()
    {
        if (_loopCts == null)
        {
            _logger.LogDebug("Outbox - notification received but loop not active");
            return;
        }

        _logger.LogDebug("Outbox - notification received");
        _asyncManualResetEvent.Set();
    }

    protected virtual Task DisposeAsyncCore() => Stop();

    protected Task Start()
    {
        if (_loopCts == null)
        {
            _logger.LogDebug("Outbox loop starting...");

            _loopCts = new CancellationTokenSource();
            _loopTask = Run();
        }
        return Task.CompletedTask;
    }

    protected async Task Stop()
    {
        _logger.LogDebug("Outbox loop stopping...");

        if (_loopCts != null)
        {
            await _loopCts.CancelAsync();
        }

        if (_loopTask != null)
        {
            await _loopTask.ConfigureAwait(false);
            _loopTask = null;
        }

        if (_loopCts != null)
        {
            _loopCts.Dispose();
            _loopCts = null;
        }
    }

    public Task OnBusLifecycle(MessageBusLifecycleEventType eventType, IMessageBus bus)
    {
        if (eventType == MessageBusLifecycleEventType.Created)
        {
            return EnsureMigrateSchema(_serviceProvider, default);
        }
        if (eventType == MessageBusLifecycleEventType.Started)
        {
            // The first started bus starts this outbox task
            if (Interlocked.Increment(ref _busStartCount) == 1)
            {
                _startBusTask = Start();
            }
            return _startBusTask;
        }
        if (eventType == MessageBusLifecycleEventType.Stopping)
        {
            // The last stopped bus stops this outbox task
            if (Interlocked.Decrement(ref _busStartCount) == 0)
            {
                _stopBusTask = Stop();
            }
            return _stopBusTask;
        }
        return Task.CompletedTask;
    }

    private static async Task MigrateSchema(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var scope = serviceProvider.CreateScope();
        try
        {
            var outboxMigrationService = scope.ServiceProvider.GetRequiredService<IOutboxMigrationService>();
            await outboxMigrationService.Migrate(cancellationToken);
        }
        catch (Exception e)
        {
            throw new MessageBusException("Outbox schema migration failed", e);
        }
        finally
        {
            if (scope is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else
            {
                scope.Dispose();
            }
        }
    }

    private Task EnsureMigrateSchema(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        lock (_migrateSchemaTaskLock)
        {
            // We optimize to ever only run once the schema migration, regardless if it was triggered from 1) bus created lifecycle or 2) outbox sending loop.
            return _migrateSchemaTask ??= MigrateSchema(serviceProvider, cancellationToken);
        }
    }

    private async Task Run()
    {
        try
        {
            _logger.LogInformation("Outbox loop started");

            var scope = _serviceProvider.CreateScope();
            try
            {
                await EnsureMigrateSchema(scope.ServiceProvider, _loopCts.Token);
                var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository<TOutboxMessage>>();
                do
                {
                    if (_loopCts.Token.IsCancellationRequested)
                    {
                        return;
                    }

                    try
                    {
                        await SendMessages(scope.ServiceProvider, outboxRepository, _loopCts.Token);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Error while processing outbox messages");
                    }
                } while (await _asyncManualResetEvent.Wait(_outboxSettings.PollIdleSleep, _loopCts.Token).ConfigureAwait(false) || !_loopCts.Token.IsCancellationRequested);
            }
            catch (TaskCanceledException)
            {
                // do nothing
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

                _logger.LogInformation("Outbox loop stopped");
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Outbox loop has crashed");
        }
    }

    async internal Task<int> SendMessages(IServiceProvider serviceProvider, IOutboxMessageRepository<TOutboxMessage> outboxRepository, CancellationToken cancellationToken)
    {
        var lockDuration = TimeSpan.FromSeconds(Math.Min(Math.Max(_outboxSettings.LockExpiration.TotalSeconds, 5), 30));
        if (lockDuration != _outboxSettings.LockExpiration)
        {
            _logger.LogWarning("Lock expiration of {OriginalLockExpiration} was outside of the acceptable range and has been clamped to {ClampedLockDuration}", _outboxSettings.LockExpiration, lockDuration);
        }

        var expirationBuffer = TimeSpan.FromSeconds(Math.Max(_outboxSettings.LockExpirationBuffer.TotalSeconds, 2));
        var lockInterval = lockDuration.Subtract(expirationBuffer);
        if (lockInterval.TotalSeconds < 3)
        {
            lockInterval = TimeSpan.FromSeconds(3);
        }

        _logger.LogDebug("Lock duration set to {LockDuration} with a lock renewal interval of {LockRenewalInterval}", lockDuration, lockInterval);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var messageBus = serviceProvider.GetRequiredService<IMessageBus>();
        var lockRenewalTimerFactory = serviceProvider.GetRequiredService<IOutboxLockRenewalTimerFactory>();
        using var lockRenewalTimer = lockRenewalTimerFactory.CreateRenewalTimer(lockDuration, lockInterval, _ => cts.Cancel(), cts.Token);

        var compositeMessageBus = messageBus as ICompositeMessageBus;
        var messageBusTarget = messageBus as IMessageBusTarget;

        var runAgain = false;
        var count = 0;
        do
        {
            _asyncManualResetEvent.Reset();
            lockRenewalTimer.Start();

            var outboxMessages = await outboxRepository.LockAndSelect(lockRenewalTimer.InstanceId, _outboxSettings.PollBatchSize, _outboxSettings.MaintainSequence, lockRenewalTimer.LockDuration, cts.Token);
            var result = await ProcessMessages(outboxRepository, outboxMessages, compositeMessageBus, messageBusTarget, cts.Token);
            runAgain = result.RunAgain;
            count += result.Count;

            lockRenewalTimer.Stop();
        } while (!cts.Token.IsCancellationRequested && runAgain);

        return count;
    }

    async internal Task<(bool RunAgain, int Count)> ProcessMessages(IOutboxMessageRepository<TOutboxMessage> outboxRepository, IReadOnlyCollection<TOutboxMessage> outboxMessages, ICompositeMessageBus compositeMessageBus, IMessageBusTarget messageBusTarget, CancellationToken cancellationToken)
    {
        const int defaultBatchSize = 50;

        var runAgain = outboxMessages.Count == _outboxSettings.PollBatchSize;
        var count = 0;

        var aborted = new List<TOutboxMessage>(_outboxSettings.PollBatchSize);
        foreach (var busGroup in outboxMessages.GroupBy(x => x.BusName))
        {
            var busName = busGroup.Key;
            var bus = GetBus(compositeMessageBus, messageBusTarget, busName);
            if (bus is not ITransportBulkProducer bulkProducer)
            {
                foreach (var outboxMessage in busGroup)
                {
                    if (bus == null)
                    {
                        _logger.LogWarning("Not able to find matching bus provider for the outbox message with Id {MessageId} of type {MessageType} to path {Path} using {BusName} bus. The message will be skipped.", outboxMessage, outboxMessage.MessageType, outboxMessage.Path, outboxMessage.BusName);
                    }
                    else
                    {
                        _logger.LogWarning("Bus provider for the outbox message with Id {MessageId} of type {MessageType} to path {Path} using {BusName} bus does not support bulk processing. The message will be skipped.", outboxMessage, outboxMessage.MessageType, outboxMessage.Path, outboxMessage.BusName);
                    }

                    aborted.Add(outboxMessage);
                }
                continue;
            }

            foreach (var pathGroup in busGroup.GroupBy(x => x.Path))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var path = pathGroup.Key;
                var messageSerializer = bus.SerializerProvider.GetSerializer(path);
                var batches = pathGroup
                    .Select(outboxMessage =>
                    {
                        var messageType = _outboxSettings.MessageTypeResolver.ToType(outboxMessage.MessageType);
                        if (messageType == null)
                        {
                            aborted.Add(outboxMessage);
                            _logger.LogError("Outbox message with Id {Id} - the MessageType {MessageType} is not recognised. The type might have been renamed or moved namespaces.", outboxMessage, outboxMessage.MessageType);
                            return null;
                        }

                        var message = messageSerializer.Deserialize(messageType, outboxMessage.MessagePayload);
                        return new OutboxBulkMessage(outboxMessage, message, messageType, outboxMessage.Headers ?? new Dictionary<string, object>());
                    })
                    .Where(x => x != null)
                    .Batch(bulkProducer.MaxMessagesPerTransaction ?? defaultBatchSize);

                foreach (var batch in batches)
                {
                    var (success, published) = await DispatchBatch(outboxRepository, bulkProducer, messageBusTarget, batch, busName, path, cancellationToken);
                    runAgain |= !success;
                    count += published;
                }
            }
        }

        if (aborted.Count > 0)
        {
            await outboxRepository.AbortDelivery(aborted, cancellationToken);
        }

        return (runAgain, count);
    }

    async internal Task<(bool Success, int Published)> DispatchBatch(IOutboxMessageRepository<TOutboxMessage> outboxRepository, ITransportBulkProducer producer, IMessageBusTarget messageBusTarget, IReadOnlyCollection<OutboxBulkMessage> batch, string busName, string path, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Publishing batch of {MessageCount} messages to pathGroup {Path} on {BusName} bus", batch.Count, path, busName);

        // TOOD: Enclose in a transaction
        var results = await producer.ProduceToTransportBulk(batch, path, messageBusTarget, cancellationToken).ConfigureAwait(false);
        if (cancellationToken.IsCancellationRequested && results.Dispatched.Count == 0)
        {
            // if cancellation has been requested, only return if no messages were published
            return (false, 0);
        }

        var updated = results.Dispatched.Select(x => x.OutboxMessage).ToHashSet();
        await outboxRepository.UpdateToSent(updated, CancellationToken.None).ConfigureAwait(false);

        if (updated.Count != batch.Count)
        {
            var failed = batch.Where(x => !updated.Contains(x.OutboxMessage!)).Select(x => x.OutboxMessage).ToHashSet();
            await outboxRepository.IncrementDeliveryAttempt(failed, _outboxSettings.MaxDeliveryAttempts, CancellationToken.None).ConfigureAwait(false);

            _logger.LogDebug("Failed to publish {MessageCount} messages in a batch of {BatchSize} to pathGroup {Path} on {BusName} bus", failed.Count, batch.Count, path, busName);
            return (false, updated.Count);
        }

        return (true, updated.Count);
    }

    private static IMasterMessageBus GetBus(ICompositeMessageBus compositeMessageBus, IMessageBusTarget messageBusTarget, string name)
    {
        if (name != null && compositeMessageBus != null)
        {
            var childBus = compositeMessageBus.GetChildBus(name);
            if (childBus != null)
            {
                return childBus;
            }
        }
        if (messageBusTarget != null)
        {
            return messageBusTarget.Target as IMasterMessageBus;
        }
        return null;
    }

    public record OutboxBulkMessage : BulkMessageEnvelope
    {
        public OutboxBulkMessage(TOutboxMessage outboxMessage, object message, Type messageType, IDictionary<string, object> headers)
            : base(message, messageType, headers)
        {
            OutboxMessage = outboxMessage;
        }

        public TOutboxMessage OutboxMessage { get; }
    }
}
