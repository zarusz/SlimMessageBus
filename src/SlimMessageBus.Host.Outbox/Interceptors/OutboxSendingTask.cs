namespace SlimMessageBus.Host.Outbox;

internal class OutboxSendingTask(
    ILoggerFactory loggerFactory,
    OutboxSettings outboxSettings,
    IServiceProvider serviceProvider)
    : IMessageBusLifecycleInterceptor, IAsyncDisposable
{
    private readonly ILogger<OutboxSendingTask> _logger = loggerFactory.CreateLogger<OutboxSendingTask>();
    private readonly OutboxSettings _outboxSettings = outboxSettings;
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    private CancellationTokenSource _loopCts;
    private Task _loopTask;

    private readonly object _migrateSchemaTaskLock = new();
    private Task _migrateSchemaTask;
    private Task _startBusTask;
    private Task _stopBusTask;

    private int _busStartCount;

    private DateTime? _cleanupNextRun;

    private bool ShouldRunCleanup()
    {
        if (_outboxSettings.MessageCleanup?.Enabled == true)
        {
            var trigger = _cleanupNextRun is null || DateTime.UtcNow > _cleanupNextRun.Value;
            if (trigger)
            {
                _cleanupNextRun = DateTime.UtcNow.Add(_outboxSettings.MessageCleanup.Interval);
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

        _loopCts?.Cancel();

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
        try
        {
            var outboxMigrationService = serviceProvider.GetRequiredService<IOutboxMigrationService>();
            await outboxMigrationService.Migrate(cancellationToken);
        }
        catch (Exception e)
        {
            throw new MessageBusException("Outbox schema migration failed", e);
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

                var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
                while (!_loopCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await SendMessages(scope.ServiceProvider, outboxRepository, _loopCts.Token);

                        if (!_loopCts.IsCancellationRequested && ShouldRunCleanup())
                        {
                            _logger.LogTrace("Running cleanup of sent messages");
                            await outboxRepository.DeleteSent(DateTime.UtcNow.Add(-_outboxSettings.MessageCleanup.Age), _loopCts.Token).ConfigureAwait(false);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Error while processing outbox messages");
                    }

                    await Task.Delay(_outboxSettings.PollIdleSleep, _loopCts.Token).ConfigureAwait(false);
                }
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

    internal async Task<int> SendMessages(IServiceProvider serviceProvider, IOutboxRepository outboxRepository, CancellationToken cancellationToken)
    {
        const int defaultBatchSize = 50;

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
        using var lockRenewalTimer = lockRenewalTimerFactory.CreateRenewalTimer(lockDuration, lockInterval, ex => { cts.Cancel(); }, cts.Token);

        var compositeMessageBus = messageBus as ICompositeMessageBus;
        var messageBusTarget = messageBus as IMessageBusTarget;

        var runAgain = false;
        var count = 0;
        do
        {
            lockRenewalTimer.Start();
            var outboxMessages = await outboxRepository.LockAndSelect(lockRenewalTimer.InstanceId, _outboxSettings.PollBatchSize, _outboxSettings.MaintainSequence, lockRenewalTimer.LockDuration, cts.Token);
            runAgain = outboxMessages.Count == _outboxSettings.PollBatchSize;
            foreach (var group in outboxMessages.GroupBy(x => x.BusName))
            {
                var busName = group.Key;
                var bus = GetBus(compositeMessageBus, messageBusTarget, busName);
                var bulkProducer = bus as IMessageBusBulkProducer;
                if (bus == null || bulkProducer == null)
                {
                    var warningMessage = bus == null
                        ? "Not able to find matching bus provider for the outbox message with Id {MessageId} of type {MessageType} to pathGroup {Path} using {BusName} bus. The message will be skipped."
                        : "Bus provider for the outbox message with Id {MessageId} of type {MessageType} to pathGroup {Path} using {BusName} bus does not support bulk processing. The message will be skipped.";

                    var ids = new List<Guid>(_outboxSettings.PollBatchSize);
                    foreach (var outboxMessage in group)
                    {
                        _logger.LogWarning(warningMessage, outboxMessage.Id, outboxMessage.MessageType.Name, outboxMessage.Path, outboxMessage.BusName);
                        ids.Add(outboxMessage.Id);
                    }

                    await outboxRepository.IncrementDeliveryAttempt(ids, _outboxSettings.MaxDeliveryAttempts, cts.Token);
                    runAgain = true;
                    continue;
                }

                foreach (var pathGroup in group.GroupBy(x => x.Path))
                {
                    if (cts.Token.IsCancellationRequested)
                    {
                        break;
                    }

                    var path = pathGroup.Key;
                    var batches = pathGroup.Select(
                        outboxMessage =>
                        {
                            var message = bus.Serializer.Deserialize(outboxMessage.MessageType, outboxMessage.MessagePayload);
                            return new EnvelopeWithId(outboxMessage.Id, message, outboxMessage.MessageType, outboxMessage.Headers ?? new Dictionary<string, object>());
                        })
                        .Batch(bulkProducer.MaxMessagesPerTransaction ?? defaultBatchSize);

                    foreach (var batch in batches)
                    {
                        var (Success, PublishedCount) = await DispatchBatchAsync(outboxRepository, bulkProducer, messageBusTarget, batch, busName, path, cts.Token);
                        runAgain |= !Success;
                        count += PublishedCount;
                    }
                }
            }

            lockRenewalTimer.Stop();
        } while (!cts.Token.IsCancellationRequested && runAgain);

        return count;
    }

    internal async Task<(bool Success, int Published)> DispatchBatchAsync(IOutboxRepository outboxRepository, IMessageBusBulkProducer producer, IMessageBusTarget messageBusTarget, IReadOnlyCollection<EnvelopeWithId> batch, string busName, string path, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Publishing batch of {MessageCount} messages to pathGroup {Path} on {BusName} bus", batch.Count, path, busName);

        // TOOD: Enclose in a transaction
        var results = await producer.ProduceToTransport(batch, path, messageBusTarget, cancellationToken).ConfigureAwait(false);
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
            return compositeMessageBus.GetChildBus(name);
        }
        if (messageBusTarget != null)
        {
            return messageBusTarget.Target as IMasterMessageBus;
        }
        return null;
    }

    public record EnvelopeWithId : Envelope
    {
        public EnvelopeWithId(Guid id, object Message, Type MessageType, IDictionary<string, object> Headers)
            : base(Message, MessageType, Headers)
        {
            Id = id;
        }

        public Guid Id { get; }
    }
}
