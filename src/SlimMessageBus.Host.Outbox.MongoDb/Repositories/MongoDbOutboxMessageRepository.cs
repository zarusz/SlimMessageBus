namespace SlimMessageBus.Host.Outbox.MongoDb.Repositories;

public partial class MongoDbOutboxMessageRepository : IMongoDbMessageOutboxRepository
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        Converters = { new ObjectToInferredTypesConverter() }
    };

    // Sentinel value meaning "no lock held". Must be earlier than any possible 'now'
    // (including FakeTimeProvider's default start of 2000-01-01) so that Lt(LockExpiresOn, now)
    // evaluates to true for every newly created message.
    private static readonly DateTime _defaultLockExpiresOn = new(1, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IMongoCollection<MongoDbOutboxDocument> _collection;
    private readonly IMongoCollection<MongoDbOutboxLockDocument> _lockCollection;
    private readonly IMongoDbTransactionService _transactionService;

    internal MongoDbOutboxMessageRepository(
        ILogger<MongoDbOutboxMessageRepository> logger,
        TimeProvider timeProvider,
        IMongoCollection<MongoDbOutboxDocument> collection,
        IMongoCollection<MongoDbOutboxLockDocument> lockCollection,
        IMongoDbTransactionService transactionService)
    {
        _logger = logger;
        _timeProvider = timeProvider;
        _collection = collection;
        _lockCollection = lockCollection;
        _transactionService = transactionService;
    }

    public async Task<OutboxMessage> Create(
        string busName,
        IDictionary<string, object> headers,
        string path,
        string messageType,
        byte[] messagePayload,
        CancellationToken cancellationToken)
    {
        var message = new MongoDbOutboxMessage
        {
            Id = Guid.NewGuid(),
            BusName = busName,
            Headers = headers,
            Path = path,
            MessageType = messageType,
            MessagePayload = messagePayload
        };

        var doc = new MongoDbOutboxDocument
        {
            Id = message.Id,
            Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
            BusName = busName,
            MessageType = messageType,
            MessagePayload = messagePayload,
            Headers = headers != null ? JsonSerializer.Serialize(headers, _jsonOptions) : null,
            Path = path,
            LockInstanceId = null,
            LockExpiresOn = _defaultLockExpiresOn,
            DeliveryAttempt = 0,
            DeliveryComplete = false,
            DeliveryAborted = false
        };

        var session = _transactionService.CurrentSession;
        if (session != null)
        {
            await _collection.InsertOneAsync(session, doc, options: null, cancellationToken);
        }
        else
        {
            await _collection.InsertOneAsync(doc, options: null, cancellationToken);
        }

        return message;
    }

    public async Task<IReadOnlyCollection<MongoDbOutboxMessage>> LockAndSelect(
        string instanceId,
        int batchSize,
        bool tableLock,
        TimeSpan lockDuration,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var lockExpiry = now.Add(lockDuration);

        if (tableLock)
        {
            var acquired = await TryAcquireTableLock(instanceId, now, lockExpiry, cancellationToken);
            if (!acquired)
            {
                return Array.Empty<MongoDbOutboxMessage>();
            }
        }

        // Build the filter for unlocked/expired messages that aren't yet delivered
        var candidateFilter = Builders<MongoDbOutboxDocument>.Filter.And(
            Builders<MongoDbOutboxDocument>.Filter.Eq(d => d.DeliveryComplete, false),
            Builders<MongoDbOutboxDocument>.Filter.Eq(d => d.DeliveryAborted, false),
            Builders<MongoDbOutboxDocument>.Filter.Or(
                Builders<MongoDbOutboxDocument>.Filter.Eq(d => d.LockInstanceId, instanceId),
                Builders<MongoDbOutboxDocument>.Filter.Lt(d => d.LockExpiresOn, now)
            )
        );

        // Find the IDs of the next batch (ordered by timestamp)
        var candidateIds = await _collection
            .Find(candidateFilter)
            .Sort(Builders<MongoDbOutboxDocument>.Sort.Ascending(d => d.Timestamp))
            .Limit(batchSize)
            .Project(Builders<MongoDbOutboxDocument>.Projection.Expression(d => d.Id))
            .ToListAsync(cancellationToken);

        if (candidateIds.Count == 0)
        {
            return Array.Empty<MongoDbOutboxMessage>();
        }

        // Atomically lock the candidates (re-applying the eligibility filter to handle races)
        var lockFilter = Builders<MongoDbOutboxDocument>.Filter.And(
            Builders<MongoDbOutboxDocument>.Filter.In(d => d.Id, candidateIds),
            Builders<MongoDbOutboxDocument>.Filter.Eq(d => d.DeliveryComplete, false),
            Builders<MongoDbOutboxDocument>.Filter.Eq(d => d.DeliveryAborted, false),
            Builders<MongoDbOutboxDocument>.Filter.Or(
                Builders<MongoDbOutboxDocument>.Filter.Eq(d => d.LockInstanceId, instanceId),
                Builders<MongoDbOutboxDocument>.Filter.Lt(d => d.LockExpiresOn, now)
            )
        );

        await _collection.UpdateManyAsync(
            lockFilter,
            Builders<MongoDbOutboxDocument>.Update
                .Set(d => d.LockInstanceId, instanceId)
                .Set(d => d.LockExpiresOn, lockExpiry),
            cancellationToken: cancellationToken);

        // Retrieve successfully locked documents
        var lockedFilter = Builders<MongoDbOutboxDocument>.Filter.And(
            Builders<MongoDbOutboxDocument>.Filter.In(d => d.Id, candidateIds),
            Builders<MongoDbOutboxDocument>.Filter.Eq(d => d.LockInstanceId, instanceId),
            Builders<MongoDbOutboxDocument>.Filter.Gt(d => d.LockExpiresOn, now),
            Builders<MongoDbOutboxDocument>.Filter.Eq(d => d.DeliveryComplete, false),
            Builders<MongoDbOutboxDocument>.Filter.Eq(d => d.DeliveryAborted, false)
        );

        var docs = await _collection
            .Find(lockedFilter)
            .Sort(Builders<MongoDbOutboxDocument>.Sort.Ascending(d => d.Timestamp))
            .ToListAsync(cancellationToken);

        return docs.Select(MapToMessage).ToList();
    }

    public async Task UpdateToSent(IReadOnlyCollection<MongoDbOutboxMessage> messages, CancellationToken cancellationToken)
    {
        if (messages.Count == 0) return;

        var ids = messages.Select(m => m.Id).ToList();
        var result = await _collection.UpdateManyAsync(
            Builders<MongoDbOutboxDocument>.Filter.In(d => d.Id, ids),
            Builders<MongoDbOutboxDocument>.Update
                .Set(d => d.DeliveryComplete, true)
                .Inc(d => d.DeliveryAttempt, 1),
            cancellationToken: cancellationToken);

        if (result.ModifiedCount != messages.Count)
        {
            throw new MessageBusException($"The number of modified documents was {result.ModifiedCount}, but {messages.Count} was expected");
        }
    }

    public async Task AbortDelivery(IReadOnlyCollection<MongoDbOutboxMessage> messages, CancellationToken cancellationToken)
    {
        if (messages.Count == 0) return;

        var ids = messages.Select(m => m.Id).ToList();
        var result = await _collection.UpdateManyAsync(
            Builders<MongoDbOutboxDocument>.Filter.In(d => d.Id, ids),
            Builders<MongoDbOutboxDocument>.Update
                .Set(d => d.DeliveryAborted, true)
                .Inc(d => d.DeliveryAttempt, 1),
            cancellationToken: cancellationToken);

        if (result.ModifiedCount != messages.Count)
        {
            throw new MessageBusException($"The number of modified documents was {result.ModifiedCount}, but {messages.Count} was expected");
        }
    }

    public async Task IncrementDeliveryAttempt(IReadOnlyCollection<MongoDbOutboxMessage> messages, int maxDeliveryAttempts, CancellationToken cancellationToken)
    {
        if (messages.Count == 0) return;

        if (maxDeliveryAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDeliveryAttempts), "Must be larger than 0.");
        }

        var ids = messages.Select(m => m.Id).ToList();
        var idFilter = Builders<MongoDbOutboxDocument>.Filter.In(d => d.Id, ids);

        // Increment delivery_attempt for all
        var incResult = await _collection.UpdateManyAsync(
            idFilter,
            Builders<MongoDbOutboxDocument>.Update.Inc(d => d.DeliveryAttempt, 1),
            cancellationToken: cancellationToken);

        if (incResult.ModifiedCount != messages.Count)
        {
            throw new MessageBusException($"The number of modified documents was {incResult.ModifiedCount}, but {messages.Count} was expected");
        }

        // Abort those that have reached or exceeded the max attempts
        await _collection.UpdateManyAsync(
            Builders<MongoDbOutboxDocument>.Filter.And(
                idFilter,
                Builders<MongoDbOutboxDocument>.Filter.Gte(d => d.DeliveryAttempt, maxDeliveryAttempts)
            ),
            Builders<MongoDbOutboxDocument>.Update.Set(d => d.DeliveryAborted, true),
            cancellationToken: cancellationToken);
    }

    public async Task<int> DeleteSent(DateTimeOffset olderThan, int batchSize, CancellationToken cancellationToken)
    {
        var olderThanUtc = olderThan.UtcDateTime;

        // Find IDs of completed (not aborted) messages older than the threshold
        var candidateIds = await _collection
            .Find(Builders<MongoDbOutboxDocument>.Filter.And(
                Builders<MongoDbOutboxDocument>.Filter.Eq(d => d.DeliveryComplete, true),
                Builders<MongoDbOutboxDocument>.Filter.Eq(d => d.DeliveryAborted, false),
                Builders<MongoDbOutboxDocument>.Filter.Lt(d => d.Timestamp, olderThanUtc)
            ))
            .Sort(Builders<MongoDbOutboxDocument>.Sort.Ascending(d => d.Timestamp))
            .Limit(batchSize)
            .Project(Builders<MongoDbOutboxDocument>.Projection.Expression(d => d.Id))
            .ToListAsync(cancellationToken);

        if (candidateIds.Count == 0)
        {
            return 0;
        }

        var deleteResult = await _collection.DeleteManyAsync(
            Builders<MongoDbOutboxDocument>.Filter.In(d => d.Id, candidateIds),
            cancellationToken);

        var deleted = (int)deleteResult.DeletedCount;
        if (deleted > 0)
            LogRemovedSentMessagesInfo(_logger, deleted);
        else
            LogRemovedSentMessagesDebug(_logger, deleted);
        return deleted;
    }

    public async Task<bool> RenewLock(string instanceId, TimeSpan lockDuration, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var newExpiry = now.Add(lockDuration);

        var result = await _collection.UpdateManyAsync(
            Builders<MongoDbOutboxDocument>.Filter.And(
                Builders<MongoDbOutboxDocument>.Filter.Eq(d => d.LockInstanceId, instanceId),
                Builders<MongoDbOutboxDocument>.Filter.Gt(d => d.LockExpiresOn, now),
                Builders<MongoDbOutboxDocument>.Filter.Eq(d => d.DeliveryComplete, false),
                Builders<MongoDbOutboxDocument>.Filter.Eq(d => d.DeliveryAborted, false)
            ),
            Builders<MongoDbOutboxDocument>.Update.Set(d => d.LockExpiresOn, newExpiry),
            cancellationToken: cancellationToken);

        return result.ModifiedCount > 0;
    }

    /// <summary>
    /// Returns all messages from the collection (for test/admin use only).
    /// </summary>
    internal async Task<IReadOnlyCollection<MongoDbOutboxAdminMessage>> GetAllMessages(CancellationToken cancellationToken)
    {
        var docs = await _collection.Find(FilterDefinition<MongoDbOutboxDocument>.Empty)
            .ToListAsync(cancellationToken);

        return docs.Select(d => new MongoDbOutboxAdminMessage
        {
            Id = d.Id,
            BusName = d.BusName,
            MessageType = d.MessageType,
            MessagePayload = d.MessagePayload,
            Headers = d.Headers == null ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(d.Headers, _jsonOptions),
            Path = d.Path,
            Timestamp = d.Timestamp,
            LockInstanceId = d.LockInstanceId,
            LockExpiresOn = d.LockExpiresOn,
            DeliveryAttempt = d.DeliveryAttempt,
            DeliveryComplete = d.DeliveryComplete,
            DeliveryAborted = d.DeliveryAborted
        }).ToList();
    }

    private MongoDbOutboxMessage MapToMessage(MongoDbOutboxDocument d) => new()
    {
        Id = d.Id,
        BusName = d.BusName,
        MessageType = d.MessageType,
        MessagePayload = d.MessagePayload,
        Headers = d.Headers == null ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(d.Headers, _jsonOptions),
        Path = d.Path
    };

    private async Task<bool> TryAcquireTableLock(string instanceId, DateTime now, DateTime lockExpiry, CancellationToken cancellationToken)
    {
        // Try to update the existing global lock document if we own it or it has expired
        var updateFilter = Builders<MongoDbOutboxLockDocument>.Filter.Or(
            Builders<MongoDbOutboxLockDocument>.Filter.Eq(d => d.LockInstanceId, instanceId),
            Builders<MongoDbOutboxLockDocument>.Filter.Lt(d => d.LockExpiresOn, now)
        );

        var updateResult = await _lockCollection.UpdateOneAsync(
            updateFilter,
            Builders<MongoDbOutboxLockDocument>.Update
                .Set(d => d.LockInstanceId, instanceId)
                .Set(d => d.LockExpiresOn, lockExpiry),
            new UpdateOptions { IsUpsert = false },
            cancellationToken);

        if (updateResult.ModifiedCount > 0)
        {
            return true;
        }

        // No document exists yet - try to insert one with our lock
        try
        {
            await _lockCollection.InsertOneAsync(
                new MongoDbOutboxLockDocument
                {
                    Id = "global",
                    LockInstanceId = instanceId,
                    LockExpiresOn = lockExpiry
                },
                options: null,
                cancellationToken);

            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // Another instance inserted before us - they hold the lock
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Removed {MessageCount} sent messages from outbox collection")]
    private static partial void LogRemovedSentMessagesInfo(ILogger logger, int messageCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Removed {MessageCount} sent messages from outbox collection")]
    private static partial void LogRemovedSentMessagesDebug(ILogger logger, int messageCount);
}
