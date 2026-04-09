namespace SlimMessageBus.Host.Outbox.MongoDb.Test;

[Trait("Category", "Integration")]
[Trait("Transport", "Outbox.MongoDb")]
public static class MongoDbOutboxRepositoryTests
{
    [Trait("Category", "Integration")]
    [Trait("Transport", "Outbox.MongoDb")]
    public class SaveTests(MongoDbFixture mongoDbFixture) : BaseMongoDbOutboxRepositoryTest(mongoDbFixture)
    {
        [Fact]
        public async Task SavedMessage_IsPersisted()
        {
            // arrange
            var message = CreateOutboxMessages(1).Single();

            // act
            message.Id = ((MongoDbOutboxMessage)await _target.Create(
                message.BusName, message.Headers, message.Path, message.MessageType, message.MessagePayload, CancellationToken.None)).Id;

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

    [Trait("Category", "Integration")]
    [Trait("Transport", "Outbox.MongoDb")]
    public class AbortDeliveryTests(MongoDbFixture mongoDbFixture) : BaseMongoDbOutboxRepositoryTest(mongoDbFixture)
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
            actual.Select(x => x.Id).Should().BeEquivalentTo(expected.Select(x => x.Id));
        }
    }

    [Trait("Category", "Integration")]
    [Trait("Transport", "Outbox.MongoDb")]
    public class DeleteSentTests(MongoDbFixture mongoDbFixture) : BaseMongoDbOutboxRepositoryTest(mongoDbFixture)
    {
        [Fact]
        public async Task ExpiredItems_AreDeleted()
        {
            // arrange
            var active = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var expired = active.AddDays(-1);

            var seedMessages = await SeedOutbox(10, (i, _) =>
            {
#pragma warning disable EXTEXP0004
                _currentTimeProvider.AdjustTime(i < 5 ? expired : active);
#pragma warning restore EXTEXP0004
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
        public async Task BatchSize_IsRespected()
        {
            const int batchSize = 10;
            const int messageCount = batchSize * 2;
            const int expectedRemainingMessages = messageCount - batchSize;

            // arrange
            var seedMessages = await SeedOutbox(messageCount);
            await _target.UpdateToSent([.. seedMessages], CancellationToken.None);

            // advance time so messages qualify for deletion
            _currentTimeProvider.Advance(TimeSpan.FromDays(1));

            // act
            var actualDeletedCount = await _target.DeleteSent(_currentTimeProvider.GetUtcNow(), batchSize, CancellationToken.None);
            var messages = await _target.GetAllMessages(CancellationToken.None);

            // assert
            actualDeletedCount.Should().Be(batchSize);
            messages.Count.Should().Be(expectedRemainingMessages);
        }
    }

    [Trait("Category", "Integration")]
    [Trait("Transport", "Outbox.MongoDb")]
    public class LockAndSelectTests(MongoDbFixture mongoDbFixture) : BaseMongoDbOutboxRepositoryTest(mongoDbFixture)
    {
        [Fact]
        public async Task TableLock_RestrictsConcurrentLocks()
        {
            const int batchSize = 10;
            const string instance1 = "1";
            const string instance2 = "2";

            await SeedOutbox(batchSize * 2);

            var items1 = await _target.LockAndSelect(instance1, batchSize, tableLock: true, TimeSpan.FromMinutes(1), CancellationToken.None);
            var items2 = await _target.LockAndSelect(instance2, batchSize, tableLock: true, TimeSpan.FromMinutes(1), CancellationToken.None);

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

            var items1 = await _target.LockAndSelect(instance1, batchSize, tableLock: false, TimeSpan.FromMinutes(1), CancellationToken.None);
            var items2 = await _target.LockAndSelect(instance2, batchSize, tableLock: false, TimeSpan.FromMinutes(1), CancellationToken.None);

            items1.Count.Should().Be(batchSize);
            items2.Count.Should().Be(batchSize);
        }

        [Fact]
        public async Task AbortedMessages_AreNotIncluded()
        {
            // arrange
            var seed = await SeedOutbox(5);
            var aborted = seed.Take(3).ToList();
            var abortedIds = aborted.Select(x => x.Id).ToList();

            await _target.AbortDelivery(aborted, CancellationToken.None);

            // act
            var actual = await _target.LockAndSelect("123", 10, tableLock: false, TimeSpan.FromMinutes(1), CancellationToken.None);

            // assert
            actual.Select(x => x.Id).Should().NotContain(abortedIds);
        }

        [Fact]
        public async Task SentMessages_AreNotIncluded()
        {
            // arrange
            var seed = await SeedOutbox(5);
            var sent = seed.Take(3).ToList();
            var sentIds = sent.Select(x => x.Id).ToList();

            await _target.UpdateToSent(sent, CancellationToken.None);

            // act
            var actual = await _target.LockAndSelect("123", 10, tableLock: false, TimeSpan.FromMinutes(1), CancellationToken.None);

            // assert
            actual.Select(x => x.Id).Should().NotContain(sentIds);
        }
    }

    [Trait("Category", "Integration")]
    [Trait("Transport", "Outbox.MongoDb")]
    public class IncrementDeliveryAttemptTests(MongoDbFixture mongoDbFixture) : BaseMongoDbOutboxRepositoryTest(mongoDbFixture)
    {
        [Fact]
        public async Task WithinMaxAttempts_DoesNotAbortDelivery()
        {
            // arrange
            const int maxAttempts = 2;
            var seed = await SeedOutbox(5);
            var failedMessages = seed.Take(3).ToList();
            var failedIds = failedMessages.Select(x => x.Id).ToList();

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
            var failedIds = failedMessages.Select(x => x.Id).ToList();

            // act - call twice to exceed max
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

    [Trait("Category", "Integration")]
    [Trait("Transport", "Outbox.MongoDb")]
    public class UpdateToSentTests(MongoDbFixture mongoDbFixture) : BaseMongoDbOutboxRepositoryTest(mongoDbFixture)
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
            actual.Select(x => x.Id).Should().BeEquivalentTo(expected.Select(x => x.Id));
        }
    }

    [Trait("Category", "Integration")]
    [Trait("Transport", "Outbox.MongoDb")]
    public class RenewLockTests(MongoDbFixture mongoDbFixture) : BaseMongoDbOutboxRepositoryTest(mongoDbFixture)
    {
        [Fact]
        public async Task WithinLock_ExtendsLockTimeout()
        {
            // arrange
            const int batchSize = 10;
            const string instanceId = "1";
            await SeedOutbox(batchSize);

            var lockedItems = await _target.LockAndSelect(instanceId, batchSize, tableLock: true, TimeSpan.FromSeconds(10), CancellationToken.None);
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

            await _target.LockAndSelect(instanceId, batchSize, tableLock: true, TimeSpan.FromSeconds(10), CancellationToken.None);

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
