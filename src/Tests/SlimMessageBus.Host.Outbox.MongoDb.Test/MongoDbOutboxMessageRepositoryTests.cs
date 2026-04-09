namespace SlimMessageBus.Host.Outbox.MongoDb.Test;

using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;

public class MongoDbOutboxMessageRepositoryTests
{
    private readonly Mock<IMongoCollection<MongoDbOutboxDocument>> _collectionMock = new();
    private readonly Mock<IMongoCollection<MongoDbOutboxLockDocument>> _lockCollectionMock = new();
    private readonly Mock<IMongoDbTransactionService> _transactionServiceMock = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly MongoDbOutboxMessageRepository _sut;

    public MongoDbOutboxMessageRepositoryTests()
    {
        _transactionServiceMock.SetupGet(x => x.CurrentSession).Returns((IClientSessionHandle?)null);

        _sut = new MongoDbOutboxMessageRepository(
            NullLogger<MongoDbOutboxMessageRepository>.Instance,
            _timeProvider,
            _collectionMock.Object,
            _lockCollectionMock.Object,
            _transactionServiceMock.Object);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static Mock<IAsyncCursor<T>> CreateCursor<T>(IReadOnlyList<T> items)
    {
        var cursor = new Mock<IAsyncCursor<T>>();
        if (items.Count > 0)
        {
            cursor.SetupSequence(x => x.MoveNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .ReturnsAsync(false);
            cursor.Setup(x => x.Current).Returns(items.ToList());
        }
        else
        {
            cursor.Setup(x => x.MoveNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        }
        cursor.Setup(x => x.MoveNext(It.IsAny<CancellationToken>())).Returns(false);
        return cursor;
    }

    private static MongoDbOutboxDocument MakeDocument(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        BusName = "bus1",
        MessageType = "TestMessage",
        MessagePayload = new byte[] { 1, 2, 3 },
        Headers = """{"h1":"v1"}""",
        Path = "path1",
        LockInstanceId = null,
        LockExpiresOn = DateTime.MinValue,
        DeliveryAttempt = 0,
        DeliveryComplete = false,
        DeliveryAborted = false,
        Timestamp = DateTime.UtcNow
    };

    private static MongoDbOutboxMessage MakeMessage(Guid? id = null) => new() { Id = id ?? Guid.NewGuid() };

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task When_Create_Given_NoActiveSession_Then_InsertsDocumentWithoutSession()
    {
        // arrange
        _collectionMock
            .Setup(x => x.InsertOneAsync(
                It.IsAny<MongoDbOutboxDocument>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // act
        var result = await _sut.Create("bus1", new Dictionary<string, object> { ["k"] = "v" }, "path1", "MsgType", new byte[] { 1 }, CancellationToken.None);

        // assert
        var msg = result.Should().BeOfType<MongoDbOutboxMessage>().Subject;
        msg.BusName.Should().Be("bus1");
        msg.Path.Should().Be("path1");
        msg.MessageType.Should().Be("MsgType");
        _collectionMock.Verify(x => x.InsertOneAsync(It.IsAny<MongoDbOutboxDocument>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task When_Create_Given_ActiveSession_Then_InsertsDocumentWithSession()
    {
        // arrange
        var sessionMock = new Mock<IClientSessionHandle>();
        _transactionServiceMock.SetupGet(x => x.CurrentSession).Returns(sessionMock.Object);

        _collectionMock
            .Setup(x => x.InsertOneAsync(
                sessionMock.Object,
                It.IsAny<MongoDbOutboxDocument>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // act
        await _sut.Create("bus1", new Dictionary<string, object>(), "path1", "MsgType", new byte[] { 1 }, CancellationToken.None);

        // assert
        _collectionMock.Verify(x => x.InsertOneAsync(sessionMock.Object, It.IsAny<MongoDbOutboxDocument>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── UpdateToSent ──────────────────────────────────────────────────────────

    [Fact]
    public async Task When_UpdateToSent_Given_EmptyList_Then_ReturnsImmediatelyWithoutDbCall()
    {
        await _sut.UpdateToSent([], CancellationToken.None);

        _collectionMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task When_UpdateToSent_Given_Messages_Then_UpdatesAllToComplete()
    {
        // arrange
        var messages = new List<MongoDbOutboxMessage> { MakeMessage(), MakeMessage() };

        _collectionMock
            .Setup(x => x.UpdateManyAsync(It.IsAny<FilterDefinition<MongoDbOutboxDocument>>(), It.IsAny<UpdateDefinition<MongoDbOutboxDocument>>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult.Acknowledged(2, 2, null));

        // act
        await _sut.UpdateToSent(messages, CancellationToken.None);

        // assert
        _collectionMock.Verify(x => x.UpdateManyAsync(It.IsAny<FilterDefinition<MongoDbOutboxDocument>>(), It.IsAny<UpdateDefinition<MongoDbOutboxDocument>>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task When_UpdateToSent_Given_ModifiedCountMismatch_Then_ThrowsMessageBusException()
    {
        // arrange
        var messages = new List<MongoDbOutboxMessage> { MakeMessage(), MakeMessage() };

        _collectionMock
            .Setup(x => x.UpdateManyAsync(It.IsAny<FilterDefinition<MongoDbOutboxDocument>>(), It.IsAny<UpdateDefinition<MongoDbOutboxDocument>>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult.Acknowledged(0, 0, null));

        // act / assert
        await ((Func<Task>)(() => _sut.UpdateToSent(messages, CancellationToken.None)))
            .Should().ThrowAsync<MessageBusException>();
    }

    // ── AbortDelivery ─────────────────────────────────────────────────────────

    [Fact]
    public async Task When_AbortDelivery_Given_EmptyList_Then_ReturnsImmediatelyWithoutDbCall()
    {
        await _sut.AbortDelivery([], CancellationToken.None);

        _collectionMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task When_AbortDelivery_Given_Messages_Then_MarksMessagesAsAborted()
    {
        // arrange
        var messages = new List<MongoDbOutboxMessage> { MakeMessage(), MakeMessage() };

        _collectionMock
            .Setup(x => x.UpdateManyAsync(It.IsAny<FilterDefinition<MongoDbOutboxDocument>>(), It.IsAny<UpdateDefinition<MongoDbOutboxDocument>>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult.Acknowledged(2, 2, null));

        // act
        await _sut.AbortDelivery(messages, CancellationToken.None);

        // assert
        _collectionMock.Verify(x => x.UpdateManyAsync(It.IsAny<FilterDefinition<MongoDbOutboxDocument>>(), It.IsAny<UpdateDefinition<MongoDbOutboxDocument>>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task When_AbortDelivery_Given_ModifiedCountMismatch_Then_ThrowsMessageBusException()
    {
        // arrange
        var messages = new List<MongoDbOutboxMessage> { MakeMessage() };

        _collectionMock
            .Setup(x => x.UpdateManyAsync(It.IsAny<FilterDefinition<MongoDbOutboxDocument>>(), It.IsAny<UpdateDefinition<MongoDbOutboxDocument>>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult.Acknowledged(0, 0, null));

        // act / assert
        await ((Func<Task>)(() => _sut.AbortDelivery(messages, CancellationToken.None)))
            .Should().ThrowAsync<MessageBusException>();
    }

    // ── IncrementDeliveryAttempt ──────────────────────────────────────────────

    [Fact]
    public async Task When_IncrementDeliveryAttempt_Given_EmptyList_Then_ReturnsImmediatelyWithoutDbCall()
    {
        await _sut.IncrementDeliveryAttempt([], maxDeliveryAttempts: 3, CancellationToken.None);

        _collectionMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task When_IncrementDeliveryAttempt_Given_InvalidMaxAttempts_Then_ThrowsArgumentOutOfRangeException()
    {
        var messages = new List<MongoDbOutboxMessage> { MakeMessage() };

        await ((Func<Task>)(() => _sut.IncrementDeliveryAttempt(messages, maxDeliveryAttempts: 0, CancellationToken.None)))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task When_IncrementDeliveryAttempt_Given_MessagesWithinMaxAttempts_Then_IncrementsAttemptForAll()
    {
        // arrange
        var messages = new List<MongoDbOutboxMessage> { MakeMessage(), MakeMessage() };

        _collectionMock
            .SetupSequence(x => x.UpdateManyAsync(It.IsAny<FilterDefinition<MongoDbOutboxDocument>>(), It.IsAny<UpdateDefinition<MongoDbOutboxDocument>>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult.Acknowledged(2, 2, null))  // inc attempt
            .ReturnsAsync(new UpdateResult.Acknowledged(0, 0, null)); // abort (none exceeded max)

        // act
        await _sut.IncrementDeliveryAttempt(messages, maxDeliveryAttempts: 5, CancellationToken.None);

        // assert: two UpdateManyAsync calls
        _collectionMock.Verify(x => x.UpdateManyAsync(It.IsAny<FilterDefinition<MongoDbOutboxDocument>>(), It.IsAny<UpdateDefinition<MongoDbOutboxDocument>>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task When_IncrementDeliveryAttempt_Given_IncModifiedCountMismatch_Then_ThrowsMessageBusException()
    {
        // arrange
        var messages = new List<MongoDbOutboxMessage> { MakeMessage(), MakeMessage() };

        _collectionMock
            .Setup(x => x.UpdateManyAsync(It.IsAny<FilterDefinition<MongoDbOutboxDocument>>(), It.IsAny<UpdateDefinition<MongoDbOutboxDocument>>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult.Acknowledged(0, 0, null));

        // act / assert
        await ((Func<Task>)(() => _sut.IncrementDeliveryAttempt(messages, maxDeliveryAttempts: 3, CancellationToken.None)))
            .Should().ThrowAsync<MessageBusException>();
    }

    // ── RenewLock ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task When_RenewLock_Given_LockedItemsExist_Then_ReturnsTrue()
    {
        // arrange
        _collectionMock
            .Setup(x => x.UpdateManyAsync(It.IsAny<FilterDefinition<MongoDbOutboxDocument>>(), It.IsAny<UpdateDefinition<MongoDbOutboxDocument>>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult.Acknowledged(3, 3, null));

        // act
        var result = await _sut.RenewLock("instance1", TimeSpan.FromMinutes(1), CancellationToken.None);

        // assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task When_RenewLock_Given_NoLockedItems_Then_ReturnsFalse()
    {
        // arrange
        _collectionMock
            .Setup(x => x.UpdateManyAsync(It.IsAny<FilterDefinition<MongoDbOutboxDocument>>(), It.IsAny<UpdateDefinition<MongoDbOutboxDocument>>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult.Acknowledged(0, 0, null));

        // act
        var result = await _sut.RenewLock("instance1", TimeSpan.FromMinutes(1), CancellationToken.None);

        // assert
        result.Should().BeFalse();
    }

    // ── DeleteSent ────────────────────────────────────────────────────────────

    [Fact]
    public async Task When_DeleteSent_Given_NoCandidates_Then_ReturnsZeroWithoutDeletion()
    {
        // arrange — FindAsync<Guid> returns empty
        _collectionMock
            .Setup(x => x.FindAsync<Guid>(It.IsAny<FilterDefinition<MongoDbOutboxDocument>>(), It.IsAny<FindOptions<MongoDbOutboxDocument, Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCursor<Guid>([]).Object);

        // act
        var deleted = await _sut.DeleteSent(_timeProvider.GetUtcNow(), batchSize: 10, CancellationToken.None);

        // assert
        deleted.Should().Be(0);
        _collectionMock.Verify(x => x.DeleteManyAsync(It.IsAny<FilterDefinition<MongoDbOutboxDocument>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task When_DeleteSent_Given_CandidatesExist_Then_DeletesThemAndReturnsCount()
    {
        // arrange
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        _collectionMock
            .Setup(x => x.FindAsync<Guid>(It.IsAny<FilterDefinition<MongoDbOutboxDocument>>(), It.IsAny<FindOptions<MongoDbOutboxDocument, Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCursor<Guid>(ids).Object);

        _collectionMock
            .Setup(x => x.DeleteManyAsync(It.IsAny<FilterDefinition<MongoDbOutboxDocument>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteResult.Acknowledged(2));

        // act
        var deleted = await _sut.DeleteSent(_timeProvider.GetUtcNow(), batchSize: 10, CancellationToken.None);

        // assert
        deleted.Should().Be(2);
        _collectionMock.Verify(x => x.DeleteManyAsync(It.IsAny<FilterDefinition<MongoDbOutboxDocument>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── LockAndSelect ─────────────────────────────────────────────────────────

    [Fact]
    public async Task When_LockAndSelect_Given_NoTableLockAndNoCandidates_Then_ReturnsEmpty()
    {
        // arrange — FindAsync<Guid> returns empty
        _collectionMock
            .Setup(x => x.FindAsync<Guid>(It.IsAny<FilterDefinition<MongoDbOutboxDocument>>(), It.IsAny<FindOptions<MongoDbOutboxDocument, Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCursor<Guid>([]).Object);

        // act
        var result = await _sut.LockAndSelect("inst1", batchSize: 5, tableLock: false, TimeSpan.FromMinutes(1), CancellationToken.None);

        // assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task When_LockAndSelect_Given_NoTableLockAndCandidatesExist_Then_ReturnsLockedMessages()
    {
        // arrange
        var candidateId = Guid.NewGuid();
        var doc = MakeDocument(candidateId);

        _collectionMock
            .Setup(x => x.FindAsync<Guid>(It.IsAny<FilterDefinition<MongoDbOutboxDocument>>(), It.IsAny<FindOptions<MongoDbOutboxDocument, Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCursor<Guid>([candidateId]).Object);

        _collectionMock
            .Setup(x => x.UpdateManyAsync(It.IsAny<FilterDefinition<MongoDbOutboxDocument>>(), It.IsAny<UpdateDefinition<MongoDbOutboxDocument>>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

        _collectionMock
            .Setup(x => x.FindAsync<MongoDbOutboxDocument>(It.IsAny<FilterDefinition<MongoDbOutboxDocument>>(), It.IsAny<FindOptions<MongoDbOutboxDocument, MongoDbOutboxDocument>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCursor<MongoDbOutboxDocument>([doc]).Object);

        // act
        var result = await _sut.LockAndSelect("inst1", batchSize: 5, tableLock: false, TimeSpan.FromMinutes(1), CancellationToken.None);

        // assert
        result.Should().HaveCount(1);
        result.Single().Id.Should().Be(candidateId);
        result.Single().BusName.Should().Be(doc.BusName);
        result.Single().Path.Should().Be(doc.Path);
    }

    [Fact]
    public async Task When_LockAndSelect_Given_TableLockAcquiredViaUpdate_Then_ProceedsWithLockAndSelect()
    {
        // arrange — UpdateOneAsync on lockCollection returns modified=1 (lock acquired)
        _lockCollectionMock
            .Setup(x => x.UpdateOneAsync(It.IsAny<FilterDefinition<MongoDbOutboxLockDocument>>(), It.IsAny<UpdateDefinition<MongoDbOutboxLockDocument>>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

        var candidateId = Guid.NewGuid();
        var doc = MakeDocument(candidateId);

        _collectionMock
            .Setup(x => x.FindAsync<Guid>(It.IsAny<FilterDefinition<MongoDbOutboxDocument>>(), It.IsAny<FindOptions<MongoDbOutboxDocument, Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCursor<Guid>([candidateId]).Object);

        _collectionMock
            .Setup(x => x.UpdateManyAsync(It.IsAny<FilterDefinition<MongoDbOutboxDocument>>(), It.IsAny<UpdateDefinition<MongoDbOutboxDocument>>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

        _collectionMock
            .Setup(x => x.FindAsync<MongoDbOutboxDocument>(It.IsAny<FilterDefinition<MongoDbOutboxDocument>>(), It.IsAny<FindOptions<MongoDbOutboxDocument, MongoDbOutboxDocument>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCursor<MongoDbOutboxDocument>([doc]).Object);

        // act
        var result = await _sut.LockAndSelect("inst1", batchSize: 5, tableLock: true, TimeSpan.FromMinutes(1), CancellationToken.None);

        // assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task When_LockAndSelect_Given_TableLockAcquiredViaInsert_Then_ProceedsWithLockAndSelect()
    {
        // arrange — UpdateOneAsync returns modified=0 (no existing lock doc), InsertOneAsync succeeds (we get the lock)
        _lockCollectionMock
            .Setup(x => x.UpdateOneAsync(It.IsAny<FilterDefinition<MongoDbOutboxLockDocument>>(), It.IsAny<UpdateDefinition<MongoDbOutboxLockDocument>>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult.Acknowledged(0, 0, null));

        _lockCollectionMock
            .Setup(x => x.InsertOneAsync(It.IsAny<MongoDbOutboxLockDocument>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var candidateId = Guid.NewGuid();
        var doc = MakeDocument(candidateId);

        _collectionMock
            .Setup(x => x.FindAsync<Guid>(It.IsAny<FilterDefinition<MongoDbOutboxDocument>>(), It.IsAny<FindOptions<MongoDbOutboxDocument, Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCursor<Guid>([candidateId]).Object);

        _collectionMock
            .Setup(x => x.UpdateManyAsync(It.IsAny<FilterDefinition<MongoDbOutboxDocument>>(), It.IsAny<UpdateDefinition<MongoDbOutboxDocument>>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

        _collectionMock
            .Setup(x => x.FindAsync<MongoDbOutboxDocument>(It.IsAny<FilterDefinition<MongoDbOutboxDocument>>(), It.IsAny<FindOptions<MongoDbOutboxDocument, MongoDbOutboxDocument>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCursor<MongoDbOutboxDocument>([doc]).Object);

        // act
        var result = await _sut.LockAndSelect("inst1", batchSize: 5, tableLock: true, TimeSpan.FromMinutes(1), CancellationToken.None);

        // assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task When_LockAndSelect_Given_TableLockNotAcquired_Then_ReturnsEmpty()
    {
        // arrange — UpdateOneAsync returns modified=0, InsertOneAsync throws DuplicateKey (another instance holds the lock)
        _lockCollectionMock
            .Setup(x => x.UpdateOneAsync(It.IsAny<FilterDefinition<MongoDbOutboxLockDocument>>(), It.IsAny<UpdateDefinition<MongoDbOutboxLockDocument>>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult.Acknowledged(0, 0, null));

        _lockCollectionMock
            .Setup(x => x.InsertOneAsync(It.IsAny<MongoDbOutboxLockDocument>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(CreateDuplicateKeyException());

        // act
        var result = await _sut.LockAndSelect("inst1", batchSize: 5, tableLock: true, TimeSpan.FromMinutes(1), CancellationToken.None);

        // assert
        result.Should().BeEmpty();
    }

    /// <summary>
    /// Creates a <see cref="MongoWriteException"/> that looks like a duplicate-key error,
    /// used to simulate a concurrent lock-document insert collision.
    /// WriteError and its constructor are internal, so we use reflection.
    /// </summary>
    private static MongoWriteException CreateDuplicateKeyException()
    {
        // WriteError(ServerErrorCategory category, int code, string message, BsonDocument details)
        var writeErrorCtor = typeof(WriteError).GetConstructors(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)[0];

        var writeError = (WriteError)writeErrorCtor.Invoke([ServerErrorCategory.DuplicateKey, 11000, "E11000 duplicate key", null]);

        // MongoWriteException(ConnectionId connectionId, WriteError writeError, WriteConcernError writeConcernError, Exception innerException)
        var connId = new ConnectionId(new ServerId(new ClusterId(), new System.Net.DnsEndPoint("localhost", 27017)));
        return new MongoWriteException(connId, writeError, null, null);
    }
}
