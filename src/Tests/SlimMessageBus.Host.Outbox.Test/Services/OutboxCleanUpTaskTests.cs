namespace SlimMessageBus.Host.Outbox.Test.Services;

public class OutboxCleanUpTaskTests
{
    public class CleanUpLoopTests : IAsyncDisposable
    {
        private readonly OutboxSettings _outboxSettings;
        private readonly Mock<IHostApplicationLifetime> _hostApplicationLifetimeMock;
        private readonly Mock<IOutboxMessageRepository<OutboxMessage>> _outboxMessageRepositoryMock;

        private readonly FakeTimeProvider _timeProvider;
        private readonly ServiceProvider _serviceProvider;

        private readonly OutboxCleanUpTaskAccessor<OutboxMessage> _outboxCleanUpTaskAccessor;

        public CleanUpLoopTests()
        {
            var logger = NullLoggerFactory.Instance.CreateLogger<OutboxCleanUpTask<OutboxMessage>>();

            _hostApplicationLifetimeMock = new Mock<IHostApplicationLifetime>();
            _outboxMessageRepositoryMock = new Mock<IOutboxMessageRepository<OutboxMessage>>();

            var services = new ServiceCollection();
            services.AddSingleton<IOutboxMessageRepository<OutboxMessage>>(_outboxMessageRepositoryMock.Object);
            _serviceProvider = services.BuildServiceProvider();

            _outboxSettings = new OutboxSettings();
            _timeProvider = new FakeTimeProvider();

            _outboxCleanUpTaskAccessor = new OutboxCleanUpTaskAccessor<OutboxMessage>(
                logger,
                _outboxSettings,
                _timeProvider,
                _hostApplicationLifetimeMock.Object,
                _serviceProvider
            );
        }

        public async ValueTask DisposeAsync()
        {
            await _serviceProvider.DisposeAsync();
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task CleanUpLoop_Should_Delete_All_Expired_Rows_Before_Sleeping()
        {
            // Arrange
            const int batchSize = 10;

            var cts = new CancellationTokenSource();

            _outboxSettings.MessageCleanup = new OutboxMessageCleanupSettings
            {
                Enabled = true,
                Age = TimeSpan.FromSeconds(10),
                Interval = TimeSpan.FromSeconds(30),
                BatchSize = batchSize
            };

            _outboxMessageRepositoryMock
                .SetupSequence(x => x.DeleteSent(It.IsAny<DateTimeOffset>(), batchSize, cts.Token))
                .Returns(Task.FromResult(batchSize))
                .Returns(Task.FromResult(batchSize))
                .Returns(Task.FromResult(batchSize / 2));

            // Act
            await _outboxCleanUpTaskAccessor.CleanUpLoop(_ => cts.Cancel(), cts.Token);

            // Assert
            _outboxMessageRepositoryMock
                .Verify(x => x.DeleteSent(It.IsAny<DateTimeOffset>(), batchSize, cts.Token), Times.Exactly(3));
        }

        [Fact]
        public async Task CleanUpLoop_Should_Delete_Again_After_Sleeping()
        {
            // Arrange
            const int batchSize = 10;
            var interval = TimeSpan.FromSeconds(30);

            var cts = new CancellationTokenSource();

            _outboxSettings.MessageCleanup = new OutboxMessageCleanupSettings
            {
                Enabled = true,
                Age = TimeSpan.FromSeconds(10),
                Interval = interval,
                BatchSize = batchSize
            };

            _outboxMessageRepositoryMock
                .SetupSequence(x => x.DeleteSent(It.IsAny<DateTimeOffset>(), batchSize, cts.Token))
                .Returns(Task.FromResult(batchSize / 2))
                .Returns(Task.FromResult(batchSize))
                .Returns(Task.FromResult(batchSize / 2));

            void sleepAction(int i)
            {
                if (i > 0)
                {
                    cts.Cancel();
                    return;
                }

                _timeProvider.Advance(interval);
            }

            // Act
            await _outboxCleanUpTaskAccessor.CleanUpLoop(sleepAction, cts.Token);

            // Assert
            _outboxMessageRepositoryMock
                .Verify(x => x.DeleteSent(It.IsAny<DateTimeOffset>(), batchSize, cts.Token), Times.Exactly(3));

            _outboxCleanUpTaskAccessor.SleepCount.Should().Be(2);
        }

        public class OutboxCleanUpTaskAccessor<TOutboxMessage>(
            ILogger<OutboxCleanUpTask<TOutboxMessage>> logger,
            OutboxSettings outboxSettings,
            TimeProvider timeProvider,
            IHostApplicationLifetime hostApplicationLifetime,
            IServiceProvider serviceProvider) : OutboxCleanUpTask<TOutboxMessage>(logger, outboxSettings, timeProvider, hostApplicationLifetime, serviceProvider)
            where TOutboxMessage : OutboxMessage
        {
            private int _sleepCount;
            private Action<int> _onSleep;

            public int SleepCount => _sleepCount;

            public Task CleanUpLoop(Action<int> onSleep, CancellationToken cancellationToken)
            {
                _sleepCount = 0;
                _onSleep = onSleep;

                return base.CleanUpLoop(cancellationToken);
            }

            protected override Task Sleep(TimeSpan delay, CancellationToken cancellationToken)
            {
                var result = base.Sleep(delay, cancellationToken);
                _onSleep?.Invoke(SleepCount);
                _sleepCount++;
                return result;
            }
        }
    }
}
