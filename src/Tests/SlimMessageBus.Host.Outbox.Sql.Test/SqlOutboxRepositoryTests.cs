namespace SlimMessageBus.Host.Outbox.Sql.Test;

[Trait("Category", "Integration")]
[Trait("Transport", "Outbox.Sql")]
public static class SqlOutboxRepositoryTests
{
    public class SaveTests : BaseSqlOutboxRepositoryTest
    {
        [Fact]
        public async Task SavedMessage_IsPersisted()
        {
            // arrange
            var message = CreateOutboxMessages(1).Single();

            // act
            message.Id = (Guid)(await _target.Create(message.BusName, message.Headers, message.Path, message.MessageType, message.MessagePayload, CancellationToken.None)).Id;
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

    public class AbortDeliveryTests : BaseSqlOutboxRepositoryTest
    {
        [Fact]
        public async Task ShouldUpdateStatus()
        {
            // arrange
            var seed = await SeedOutbox(5);
            var expected = seed.Select(x => x.Id).Take(3).ToList();

            // act
            await _target.AbortDelivery(expected, CancellationToken.None);
            var messages = await _target.GetAllMessages(CancellationToken.None);

            // assert
            var actual = messages.Where(x => x.DeliveryAborted).Select(x => x.Id).ToList();
            actual.Should().BeEquivalentTo(expected);
        }
    }

    public class DeleteSentTests : BaseSqlOutboxRepositoryTest
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
            await _target.UpdateToSent(seedMessages.Select(x => x.Id).Take(5).ToList(), CancellationToken.None);

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
            await _target.UpdateToSent([.. seedMessages.Select(x => x.Id)], CancellationToken.None);

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

    public class LockAndSelectTests : BaseSqlOutboxRepositoryTest
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
            var abortedIds = seed.Select(x => x.Id).Take(3).ToList();

            await _target.AbortDelivery(abortedIds, CancellationToken.None);

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
            var sentIds = seed.Select(x => x.Id).Take(3).ToList();

            await _target.UpdateToSent(sentIds, CancellationToken.None);

            // act
            var actual = await _target.LockAndSelect("123", 10, false, TimeSpan.FromMinutes(1), CancellationToken.None);

            // assert
            actual.Select(x => x.Id).Should().NotContain(sentIds);
        }
    }

    public class IncrementDeliveryAttemptTests : BaseSqlOutboxRepositoryTest
    {
        [Fact]
        public async Task WithinMaxAttempts_DoesNotAbortDelivery()
        {
            // arrange
            const int maxAttempts = 2;
            var seed = await SeedOutbox(5);
            var ids = seed.Select(x => x.Id).Take(3).ToList();

            // act
            await _target.IncrementDeliveryAttempt(ids, maxAttempts, CancellationToken.None);
            var messages = await _target.GetAllMessages(CancellationToken.None);

            // assert
            messages.Should().OnlyContain(x => !x.DeliveryComplete);
            messages.Should().OnlyContain(x => !x.DeliveryAborted);
            messages.Where(x => !ids.Contains(x.Id)).Should().OnlyContain(x => x.DeliveryAttempt == 0);
            messages.Where(x => ids.Contains(x.Id)).Should().OnlyContain(x => x.DeliveryAttempt == 1);
        }

        [Fact]
        public async Task BreachingMaxAttempts_AbortsDelivery()
        {
            // arrange
            const int maxAttempts = 1;
            var seed = await SeedOutbox(5);
            var ids = seed.Select(x => x.Id).Take(3).ToList();

            // act
            await _target.IncrementDeliveryAttempt(ids, maxAttempts, CancellationToken.None);
            await _target.IncrementDeliveryAttempt(ids, maxAttempts, CancellationToken.None);
            var messages = await _target.GetAllMessages(CancellationToken.None);

            // assert
            messages.Should().OnlyContain(x => !x.DeliveryComplete);

            var attempted = messages.Where(x => ids.Contains(x.Id)).ToList();
            attempted.Should().OnlyContain(x => x.DeliveryAttempt == 2);
            attempted.Should().OnlyContain(x => x.DeliveryAborted);

            var notAttempted = messages.Where(x => !ids.Contains(x.Id)).ToList();
            notAttempted.Should().OnlyContain(x => x.DeliveryAttempt == 0);
            notAttempted.Should().OnlyContain(x => !x.DeliveryAborted);
        }
    }

    public class UpdateToSentTests : BaseSqlOutboxRepositoryTest
    {
        [Fact]
        public async Task ShouldUpdateStatus()
        {
            // arrange
            var seed = await SeedOutbox(5);
            var expected = seed.Select(x => x.Id).Take(3).ToList();

            // act
            await _target.UpdateToSent(expected, CancellationToken.None);
            var messages = await _target.GetAllMessages(CancellationToken.None);

            // assert
            var actual = messages.Where(x => x.DeliveryComplete).Select(x => x.Id).ToList();
            actual.Should().BeEquivalentTo(expected);
        }
    }

    public class RenewLockTests : BaseSqlOutboxRepositoryTest
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