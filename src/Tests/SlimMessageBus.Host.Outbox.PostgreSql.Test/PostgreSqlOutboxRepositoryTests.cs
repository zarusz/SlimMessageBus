namespace SlimMessageBus.Host.Outbox.PostgreSql.Test;

[Trait("Category", "Integration")]
[Trait("Transport", "Outbox.Sql")]
public static class PostgreSqlOutboxRepositoryTests
{
    public class SaveTests(PostgreSqlFixture postgreSqlFixture) : BasePostgreSqlOutboxRepositoryTest(postgreSqlFixture)
    {
        [Fact]
        public async Task SavedMessage_IsPersisted()
        {
            // arrange
            var message = CreateOutboxMessages(1).Single();

            // act
            message.Id = ((PostgreSqlOutboxMessage)await _target.Create(message.BusName, message.Headers, message.Path, message.MessageType, message.MessagePayload, CancellationToken.None)).Id;
            var messages = await _target.GetAllMessages(CancellationToken.None);

            // assert
            messages.Count.Should().Be(1);
            var actual = messages.Single();
            actual.Id.Should().Be(message.Id);
            actual.BusName.Should().Be(message.BusName);
            actual.Headers.Should().BeEquivalentTo(message.Headers);
            actual.Path.Should().Be(message.Path);
            actual.MessageType.Should().Be(message.MessageType);
            actual.MessagePayload.Should().BeEquivalentTo(message.MessagePayload);
        }
    }

    public class AbortDeliveryTests(PostgreSqlFixture postgreSqlFixture) : BasePostgreSqlOutboxRepositoryTest(postgreSqlFixture)
    {
        [Fact]
        public async Task ShouldUpdateStatus()
        {
            // arrange
            var seed = await SeedOutbox(5);
            var expected = seed.Take(3).ToList();

            // act
            await _target.AbortDelivery(expected, CancellationToken.None);
            var messages = await _target.GetAllMessages(CancellationToken.None);

            // assert
            var actual = messages.Where(x => x.DeliveryAborted).ToList();
            actual.Should().BeEquivalentTo(expected);
        }
    }

    public class DeleteSentTests(PostgreSqlFixture postgreSqlFixture) : BasePostgreSqlOutboxRepositoryTest(postgreSqlFixture)
    {
        [Fact]
        public async Task ExpiredItems_AreDeleted()
        {
            // arrange
            var active = new DateTime(2000, 1, 1);
            var expired = active.AddDays(-1);

            var seedMessages = await SeedOutbox(10, (i, x) =>
            {
                // affect the timestamp to make the message expired
#pragma warning disable EXTEXP0004 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                _currentTimeProvider.AdjustTime(i < 5 ? expired : active);
#pragma warning restore EXTEXP0004 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            });

            // mark the first 5 messages as sent
            await _target.UpdateToSent(seedMessages.Take(5).ToList(), CancellationToken.None);

            // act
            await _target.DeleteSent(active, 10, CancellationToken.None);
            var messages = await _target.GetAllMessages(CancellationToken.None);

            // assert
            messages.Should().OnlyContain(x => x.Timestamp == active);
        }

        [Fact]
        public async Task BatchSize_IsDeleted()
        {
            const int batchSize = 10;
            const int messageCount = batchSize * 2;
            const int expectedRemainingMessages = messageCount - batchSize;

            // arrange
            var seedMessages = await SeedOutbox(messageCount);

            // mark all as sent
            await _target.UpdateToSent([.. seedMessages], CancellationToken.None);

            // advance time to allow messages to expire
            _currentTimeProvider.Advance(TimeSpan.FromDays(1));

            // act
            var actualDeletedCount = await _target.DeleteSent(_currentTimeProvider.GetUtcNow(), batchSize, CancellationToken.None);
            var messages = await _target.GetAllMessages(CancellationToken.None);

            // assert
            actualDeletedCount.Should().Be(batchSize);
            messages.Count.Should().Be(expectedRemainingMessages);
        }
    }

    public class LockAndSelectTests(PostgreSqlFixture postgreSqlFixture) : BasePostgreSqlOutboxRepositoryTest(postgreSqlFixture)
    {
        [Fact]
        public async Task TableLock_RestrictsConcurrentLocks()
        {
            const int batchSize = 10;

            const string instance1 = "1";
            const string instance2 = "2";

            await SeedOutbox(batchSize * 2);

            var items1 = await _target.LockAndSelect(instance1, batchSize, true, TimeSpan.FromMinutes(1), CancellationToken.None);
            var items2 = await _target.LockAndSelect(instance2, batchSize, true, TimeSpan.FromMinutes(1), CancellationToken.None);

            items1.Count.Should().Be(batchSize);
            items2.Count.Should().Be(0);
        }

        [Fact]
        public async Task NoTableLock_AllowsConcurrentLocks()
        {
            const int batchSize = 10;

            const string instance1 = "1";
            const string instance2 = "2";

            await SeedOutbox(batchSize * 2);

            var items1 = await _target.LockAndSelect(instance1, batchSize, false, TimeSpan.FromMinutes(1), CancellationToken.None);
            var items2 = await _target.LockAndSelect(instance2, batchSize, false, TimeSpan.FromMinutes(1), CancellationToken.None);

            items1.Count.Should().Be(batchSize);
            items2.Count.Should().Be(batchSize);
        }

        [Fact]
        public async Task AbortedMessages_AreNotIncluded()
        {
            // arrange
            var seed = await SeedOutbox(5);
            var aborted = seed.Take(3).ToList();
            var abortedIds = aborted.ConvertAll(x => x.Id);

            await _target.AbortDelivery(aborted, CancellationToken.None);

            // act
            var actual = await _target.LockAndSelect("123", 10, false, TimeSpan.FromMinutes(1), CancellationToken.None);

            // assert
            actual.Select(x => x.Id).Should().NotContain(abortedIds);
        }

        [Fact]
        public async Task SentMessages_AreNotIncluded()
        {
            // arrange
            var seed = await SeedOutbox(5);
            var sent = seed.Take(3).ToList();
            var sentIds = sent.ConvertAll(x => x.Id);

            await _target.UpdateToSent(sent, CancellationToken.None);

            // act
            var actual = await _target.LockAndSelect("123", 10, false, TimeSpan.FromMinutes(1), CancellationToken.None);

            // assert
            actual.Select(x => x.Id).Should().NotContain(sentIds);
        }
    }

    public class SendMessagesTests(PostgreSqlFixture postgreSqlFixture) : BasePostgreSqlOutboxRepositoryTest(postgreSqlFixture)
    {
        private const string InstanceId = "outbox-sender";

        [Fact]
        public async Task FailedBatch_IsNotRetriedImmediatelyWithSameLockInstance()
        {
            // arrange
            const int messageCount = 5;
            const int maxDeliveryAttempts = 3;

            _settings.PollBatchSize = messageCount;
            _settings.MaxDeliveryAttempts = maxDeliveryAttempts;
            _settings.LockExpiration = TimeSpan.FromSeconds(5);

            await SeedOutbox(messageCount, (i, message) =>
            {
                message.MessageType = typeof(TestOutboxMessage).AssemblyQualifiedName;
                message.MessagePayload = JsonSerializer.SerializeToUtf8Bytes(new TestOutboxMessage(i));
            });

            var serializer = new Mock<IMessageSerializer>();
            serializer
                .Setup(x => x.Deserialize(typeof(TestOutboxMessage), It.IsAny<IReadOnlyDictionary<string, object>>(), It.IsAny<byte[]>(), null))
                .Returns((Type _, IReadOnlyDictionary<string, object> _, byte[] payload, object _) => JsonSerializer.Deserialize<TestOutboxMessage>(payload));

            var serializerProvider = new Mock<IMessageSerializerProvider>();
            serializerProvider.Setup(x => x.GetSerializer(It.IsAny<string>())).Returns(serializer.Object);

            var masterBus = new Mock<IMasterMessageBus>();
            masterBus.Setup(x => x.SerializerProvider).Returns(serializerProvider.Object);

            var bulkProducer = masterBus.As<ITransportBulkProducer>();
            bulkProducer.Setup(x => x.MaxMessagesPerTransaction).Returns((int?)null);
            bulkProducer
                .Setup(x => x.ProduceToTransportBulk(It.IsAny<IReadOnlyCollection<OutboxSendingTask<PostgreSqlOutboxMessage>.OutboxBulkMessage>>(), It.IsAny<string>(), It.IsAny<IMessageBusTarget>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProduceToTransportBulkResult<OutboxSendingTask<PostgreSqlOutboxMessage>.OutboxBulkMessage>([], new ProducerMessageBusException("Broker unavailable")));

            var messageBus = new Mock<IMessageBusTarget>();
            messageBus.Setup(x => x.Target).Returns(masterBus.Object);

            var lockRenewalTimer = new Mock<IOutboxLockRenewalTimer>();
            lockRenewalTimer.Setup(x => x.InstanceId).Returns(InstanceId);
            lockRenewalTimer.Setup(x => x.LockDuration).Returns(_settings.LockExpiration);

            var lockRenewalTimerFactory = new Mock<IOutboxLockRenewalTimerFactory>();
            lockRenewalTimerFactory
                .Setup(x => x.CreateRenewalTimer(It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<Action<Exception>>(), It.IsAny<CancellationToken>()))
                .Returns(lockRenewalTimer.Object);

            var services = new ServiceCollection()
                .AddSingleton<IMessageBus>(messageBus.Object)
                .AddSingleton(lockRenewalTimerFactory.Object)
                .BuildServiceProvider();

            var target = new OutboxSendingTask<PostgreSqlOutboxMessage>(NullLoggerFactory.Instance, _settings, services);

            // act
            var published = await target.SendMessages(services, _target, CancellationToken.None);
            var messages = await _target.GetAllMessages(CancellationToken.None);

            // assert
            published.Should().Be(0);
            messages.Should().OnlyContain(x => x.DeliveryAttempt == 1);
            messages.Should().OnlyContain(x => !x.DeliveryAborted);
        }

        private sealed record TestOutboxMessage(int Value);
    }

    public class IncrementDeliveryAttemptTests(PostgreSqlFixture postgreSqlFixture) : BasePostgreSqlOutboxRepositoryTest(postgreSqlFixture)
    {
        [Fact]
        public async Task WithinMaxAttempts_DoesNotAbortDelivery()
        {
            // arrange
            const int maxAttempts = 2;
            var seed = await SeedOutbox(5);
            var failedMessages = seed.Take(3).ToList();
            var failedIds = failedMessages.ConvertAll(x => x.Id);

            // act
            await _target.IncrementDeliveryAttempt(failedMessages, maxAttempts, CancellationToken.None);
            var messages = await _target.GetAllMessages(CancellationToken.None);

            // assert
            messages.Should().OnlyContain(x => !x.DeliveryComplete);
            messages.Should().OnlyContain(x => !x.DeliveryAborted);

            messages.Where(x => !failedIds.Contains(x.Id)).Should().OnlyContain(x => x.DeliveryAttempt == 0);
            messages.Where(x => failedIds.Contains(x.Id)).Should().OnlyContain(x => x.DeliveryAttempt == 1);
        }

        [Fact]
        public async Task BreachingMaxAttempts_AbortsDelivery()
        {
            // arrange
            const int maxAttempts = 1;
            var seed = await SeedOutbox(5);
            var failedMessages = seed.Take(3).ToList();
            var failedIds = failedMessages.ConvertAll(x => x.Id);

            // act
            await _target.IncrementDeliveryAttempt(failedMessages, maxAttempts, CancellationToken.None);
            await _target.IncrementDeliveryAttempt(failedMessages, maxAttempts, CancellationToken.None);
            var messages = await _target.GetAllMessages(CancellationToken.None);

            // assert
            messages.Should().OnlyContain(x => !x.DeliveryComplete);

            var attempted = messages.Where(x => failedIds.Contains(x.Id)).ToList();
            attempted.Should().OnlyContain(x => x.DeliveryAttempt == 2);
            attempted.Should().OnlyContain(x => x.DeliveryAborted);

            var notAttempted = messages.Where(x => !failedIds.Contains(x.Id)).ToList();
            notAttempted.Should().OnlyContain(x => x.DeliveryAttempt == 0);
            notAttempted.Should().OnlyContain(x => !x.DeliveryAborted);
        }
    }

    public class UpdateToSentTests(PostgreSqlFixture postgreSqlFixture) : BasePostgreSqlOutboxRepositoryTest(postgreSqlFixture)
    {
        [Fact]
        public async Task ShouldUpdateStatus()
        {
            // arrange
            var seed = await SeedOutbox(5);
            var expected = seed.Take(3).ToList();

            // act
            await _target.UpdateToSent(expected, CancellationToken.None);
            var messages = await _target.GetAllMessages(CancellationToken.None);

            // assert
            var actual = messages.Where(x => x.DeliveryComplete).ToList();
            actual.Should().BeEquivalentTo(expected);
        }
    }

    public class RenewLockTests(PostgreSqlFixture postgreSqlFixture) : BasePostgreSqlOutboxRepositoryTest(postgreSqlFixture)
    {
        [Fact]
        public async Task WithinLock_ExtendsLockTimeout()
        {
            // arrange
            const int batchSize = 10;
            const string instanceId = "1";
            await SeedOutbox(batchSize);

            var lockedItems = await _target.LockAndSelect(instanceId, batchSize, true, TimeSpan.FromSeconds(10), CancellationToken.None);
            var lockedIds = lockedItems.Select(x => x.Id).ToList();

            var before = await _target.GetAllMessages(CancellationToken.None);
            var originalLock = before.Min(x => x.LockExpiresOn);

            // act
            await _target.RenewLock(instanceId, TimeSpan.FromMinutes(1), CancellationToken.None);

            // assert
            var after = await _target.GetAllMessages(CancellationToken.None);
            var actual = after.Where(x => lockedIds.Contains(x.Id));

            actual.Should().OnlyContain(x => x.LockExpiresOn > originalLock);
        }

        [Fact]
        public async Task HasLockedItemsToRenew_ReturnsTrue()
        {
            // arrange
            const int batchSize = 10;
            const string instanceId = "1";
            await SeedOutbox(batchSize);

            await _target.LockAndSelect(instanceId, batchSize, true, TimeSpan.FromSeconds(10), CancellationToken.None);

            // act
            var actual = await _target.RenewLock(instanceId, TimeSpan.FromMinutes(1), CancellationToken.None);

            // assert
            actual.Should().BeTrue();
        }

        [Fact]
        public async Task HasNoLockedItemsToRenew_ReturnsFalse()
        {
            // arrange
            const string instanceId = "1";
            await SeedOutbox(10);

            // act
            var actual = await _target.RenewLock(instanceId, TimeSpan.FromMinutes(1), CancellationToken.None);

            // assert
            actual.Should().BeFalse();
        }
    }
}
