namespace SlimMessageBus.Host.Outbox.Services;

internal class OutboxSendingTask<TOutboxMessage, TOutboxMessageKey>(
    ILoggerFactory loggerFactory,
    OutboxSettings outboxSettings,
    ICurrentTimeProvider currentTimeProvider,
    IServiceProvider serviceProvider)
    : IMessageBusLifecycleInterceptor, IOutboxNotificationService, IAsyncDisposable
    where TOutboxMessage : OutboxMessage<TOutboxMessageKey>
{
    private readonly ILogger<OutboxSendingTask<TOutboxMessage, TOutboxMessageKey>> _logger = loggerFactory.CreateLogger<OutboxSendingTask<TOutboxMessage, TOutboxMessageKey>>();
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

    private DateTimeOffset? _cleanupNextRun;

    private bool ShouldRunCleanup()
    {
        if (_outboxSettings.MessageCleanup?.Enabled == true)
        {
            var currentTime = currentTimeProvider.CurrentTime;
            var trigger = !_cleanupNextRun.HasValue || currentTime > _cleanupNextRun.Value;
            if (trigger)
            {
                _cleanupNextRun = currentTime.Add(_outboxSettings.MessageCleanup.Interval);
            }

            return trigger;
        }
        return false;
    }

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
                var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository<TOutboxMessage, TOutboxMessageKey>>();
                do
                {
                    if (_loopCts.Token.IsCancellationRequested)
                    {
                        return;
                    }

                    try
                    {
                        await SendMessages(scope.ServiceProvider, outboxRepository, _loopCts.Token);

                        if (!_loopCts.IsCancellationRequested && ShouldRunCleanup())
                        {
                            _logger.LogTrace("Running cleanup of sent messages");
                            await outboxRepository.DeleteSent(currentTimeProvider.CurrentTime.DateTime.Add(-_outboxSettings.MessageCleanup.Age), _loopCts.Token).ConfigureAwait(false);
                        }
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

    async internal Task<int> SendMessages(IServiceProvider serviceProvider, IOutboxMessageRepository<TOutboxMessage, TOutboxMessageKey> outboxRepository, CancellationToken cancellationToken)
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

    async internal Task<(bool RunAgain, int Count)> ProcessMessages(IOutboxMessageRepository<TOutboxMessage, TOutboxMessageKey> outboxRepository, IReadOnlyCollection<TOutboxMessage> outboxMessages, ICompositeMessageBus compositeMessageBus, IMessageBusTarget messageBusTarget, CancellationToken cancellationToken)
    {
        const int defaultBatchSize = 50;

        var runAgain = outboxMessages.Count == _outboxSettings.PollBatchSize;
        var count = 0;

        var abortedIds = new List<TOutboxMessageKey>(_outboxSettings.PollBatchSize);
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
                        _logger.LogWarning("Not able to find matching bus provider for the outbox message with Id {MessageId} of type {MessageType} to path {Path} using {BusName} bus. The message will be skipped.", outboxMessage.Id, outboxMessage.MessageType, outboxMessage.Path, outboxMessage.BusName);
                    }
                    else
                    {
                        _logger.LogWarning("Bus provider for the outbox message with Id {MessageId} of type {MessageType} to path {Path} using {BusName} bus does not support bulk processing. The message will be skipped.", outboxMessage.Id, outboxMessage.MessageType, outboxMessage.Path, outboxMessage.BusName);
                    }

                    abortedIds.Add(outboxMessage.Id);
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
                var batches = pathGroup
                    .Select(outboxMessage =>
                    {
                        var messageType = _outboxSettings.MessageTypeResolver.ToType(outboxMessage.MessageType);
                        if (messageType == null)
                        {
                            abortedIds.Add(outboxMessage.Id);
                            _logger.LogError("Outbox message with Id {Id} - the MessageType {MessageType} is not recognized. The type might have been renamed or moved namespaces.", outboxMessage.Id, outboxMessage.MessageType);
                            return null;
                        }

                        var message = bus.Serializer.Deserialize(messageType, outboxMessage.MessagePayload);
                        return new OutboxBulkMessage(outboxMessage.Id, message, messageType, outboxMessage.Headers ?? new Dictionary<string, object>());
                    })
                    .Where(x => x != null)
                    .Batch(bulkProducer.MaxMessagesPerTransaction ?? defaultBatchSize);

                foreach (var batch in batches)
                {
                    var result = await DispatchBatch(outboxRepository, bulkProducer, messageBusTarget, batch, busName, path, cancellationToken);
                    runAgain |= !result.Success;
                    count += result.Published;
                }
            }
        }

        if (abortedIds.Count > 0)
        {
            await outboxRepository.AbortDelivery(abortedIds, cancellationToken);
        }

        return (runAgain, count);
    }

    async internal Task<(bool Success, int Published)> DispatchBatch(IOutboxMessageRepository<TOutboxMessage, TOutboxMessageKey> outboxRepository, ITransportBulkProducer producer, IMessageBusTarget messageBusTarget, IReadOnlyCollection<OutboxBulkMessage> batch, string busName, string path, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Publishing batch of {MessageCount} messages to pathGroup {Path} on {BusName} bus", batch.Count, path, busName);

        // TOOD: Enclose in a transaction
        var results = await producer.ProduceToTransportBulk(batch, path, messageBusTarget, cancellationToken).ConfigureAwait(false);
        if (cancellationToken.IsCancellationRequested && results.Dispatched.Count == 0)
        {
            // if cancellation has been requested, only return if no messages were published
            return (false, 0);
        }

        var updatedIds = results.Dispatched.Select(x => x.Id).ToHashSet();
        await outboxRepository.UpdateToSent(updatedIds, CancellationToken.None).ConfigureAwait(false);

        if (updatedIds.Count != batch.Count)
        {
            var failedIds = batch.Where(x => !updatedIds.Contains(x.Id!)).Select(x => x.Id).ToHashSet();
            await outboxRepository.IncrementDeliveryAttempt(failedIds, _outboxSettings.MaxDeliveryAttempts, CancellationToken.None).ConfigureAwait(false);

            _logger.LogDebug("Failed to publish {MessageCount} messages in a batch of {BatchSize} to pathGroup {Path} on {BusName} bus", failedIds.Count, batch.Count, path, busName);
            return (false, updatedIds.Count);
        }

        return (true, updatedIds.Count);
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
        public TOutboxMessageKey Id { get; }

        public OutboxBulkMessage(TOutboxMessageKey id, object message, Type messageType, IDictionary<string, object> headers)
            : base(message, messageType, headers)
        {
            Id = id;
        }
    }
}
