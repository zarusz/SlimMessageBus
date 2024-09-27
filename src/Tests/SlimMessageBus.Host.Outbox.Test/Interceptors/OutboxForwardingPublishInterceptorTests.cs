namespace SlimMessageBus.Host.Outbox.Test.Interceptors;

using SlimMessageBus.Host.Outbox.Services;
using SlimMessageBus.Host.Serialization;

public static class OutboxForwardingPublishInterceptorTests
{
    public class OrderTests
    {
        [Fact]
        public void OutboxForwardingPublisher_MustBeLastInPipeline()
        {
            // arrange
            var expected = int.MaxValue;

            var mockLogger = new Mock<ILogger<OutboxForwardingPublishInterceptor>>();
            var mockOutboxRepository = new Mock<IOutboxRepository<Guid>>();
            var mockInstanceIdProvider = new Mock<IInstanceIdProvider>();
            var mockOutboxNotificationService = new Mock<IOutboxNotificationService>();
            var mockOutboxSettings = new Mock<OutboxSettings>();

            // act
            var target = new OutboxForwardingPublishInterceptor<object, Guid>(mockLogger.Object, mockOutboxRepository.Object, mockInstanceIdProvider.Object, mockOutboxNotificationService.Object, mockOutboxSettings.Object);
            var actual = target.Order;

            // assert
            actual.Should().Be(expected);
        }

        [Fact]
        public void OutboxForwardingPublisher_MustImplement_IInterceptorWithOrder()
        {
            // act
            var actual = typeof(OutboxForwardingPublishInterceptor<object, Guid>).IsAssignableTo(typeof(IInterceptorWithOrder));

            // assert
            actual.Should().BeTrue();
        }
    }

    public class OnHandleTests
    {
        private readonly Mock<ILogger<OutboxForwardingPublishInterceptor>> _mockLogger;
        private readonly Mock<IOutboxRepository<Guid>> _mockOutboxRepository;
        private readonly Mock<IInstanceIdProvider> _mockInstanceIdProvider;
        private readonly Mock<IMessageSerializer> _mockSerializer;
        private readonly Mock<IMasterMessageBus> _mockMasterMessageBus;
        private readonly Mock<IOutboxNotificationService> _mockOutboxNotificationService;
        private readonly Mock<OutboxSettings> _mockOutboxSettings;
        private readonly Mock<IMessageBusTarget> _mockTargetBus;
        private readonly Mock<IProducerContext> _mockProducerContext;

        public OnHandleTests()
        {
            _mockLogger = new Mock<ILogger<OutboxForwardingPublishInterceptor>>();
            _mockOutboxRepository = new Mock<IOutboxRepository<Guid>>();
            _mockInstanceIdProvider = new Mock<IInstanceIdProvider>();
            _mockOutboxNotificationService = new Mock<IOutboxNotificationService>();
            _mockOutboxSettings = new Mock<OutboxSettings>();

            _mockSerializer = new Mock<IMessageSerializer>();

            _mockMasterMessageBus = new Mock<IMasterMessageBus>();
            _mockMasterMessageBus.SetupGet(x => x.Serializer).Returns(_mockSerializer.Object).Verifiable();

            _mockTargetBus = new Mock<IMessageBusTarget>();
            _mockTargetBus.SetupGet(x => x.Target).Returns(_mockMasterMessageBus.Object);

            _mockProducerContext = new Mock<IProducerContext>();
            _mockProducerContext.SetupGet(x => x.Bus).Returns(_mockTargetBus.Object);
        }

        [Fact]
        public async Task SkipOutboxHeader_IsPresent_PromoteToNext()
        {
            // arrange
            _mockProducerContext.SetupGet(x => x.Headers).Returns(new Dictionary<string, object> { { OutboxForwardingPublishInterceptor<object, Guid>.SkipOutboxHeader, true } });
            _mockOutboxRepository.Setup(x => x.Save(It.IsAny<OutboxMessage<Guid>>(), It.IsAny<CancellationToken>())).Verifiable();

            var nextCalled = 0;
            var next = () =>
            {
                nextCalled++;
                return Task.CompletedTask;
            };

            // act
            var target = new OutboxForwardingPublishInterceptor<object, Guid>(_mockLogger.Object, _mockOutboxRepository.Object, _mockInstanceIdProvider.Object, _mockOutboxNotificationService.Object, _mockOutboxSettings.Object);
            await target.OnHandle(new object(), next, _mockProducerContext.Object);
            target.Dispose();

            // assert
            _mockProducerContext.VerifyGet(x => x.Bus, Times.AtLeastOnce);
            _mockProducerContext.VerifyGet(x => x.Headers, Times.AtLeastOnce);
            _mockOutboxRepository.Verify(x => x.Save(It.IsAny<OutboxMessage<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);

            nextCalled.Should().Be(1);
        }

        [Fact]
        public async Task SkipOutboxHeader_IsNotPresent_DoNotPromoteToNext()
        {
            // arrange
            var message = new object();

            _mockSerializer.Setup(x => x.Serialize(typeof(object), message)).Verifiable();
            _mockOutboxRepository.Setup(x => x.Save(It.IsAny<OutboxMessage<Guid>>(), It.IsAny<CancellationToken>())).Verifiable();

            var nextCalled = 0;
            var next = () =>
            {
                nextCalled++;
                return Task.CompletedTask;
            };

            // act
            var target = new OutboxForwardingPublishInterceptor<object, Guid>(_mockLogger.Object, _mockOutboxRepository.Object, _mockInstanceIdProvider.Object, _mockOutboxNotificationService.Object, _mockOutboxSettings.Object);
            await target.OnHandle(new object(), next, _mockProducerContext.Object);
            target.Dispose();

            // assert
            nextCalled.Should().Be(0);
            _mockOutboxRepository.Verify(x => x.Save(It.IsAny<OutboxMessage<Guid>>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SkipOutboxHeader_IsPresent_DoNotRaiseOutboxNotification()
        {
            // arrange
            _mockProducerContext.SetupGet(x => x.Headers).Returns(new Dictionary<string, object> { { OutboxForwardingPublishInterceptor<object, Guid>.SkipOutboxHeader, true } });
            _mockOutboxRepository.Setup(x => x.Save(It.IsAny<OutboxMessage<Guid>>(), It.IsAny<CancellationToken>())).Verifiable();
            _mockOutboxNotificationService.Setup(x => x.Notify()).Verifiable();

            // act
            var target = new OutboxForwardingPublishInterceptor<object, Guid>(_mockLogger.Object, _mockOutboxRepository.Object, _mockInstanceIdProvider.Object, _mockOutboxNotificationService.Object, _mockOutboxSettings.Object);
            await target.OnHandle(new object(), () => Task.CompletedTask, _mockProducerContext.Object);
            target.Dispose();

            // assert
            _mockOutboxRepository.Verify(x => x.Save(It.IsAny<OutboxMessage<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
            _mockOutboxNotificationService.Verify(x => x.Notify(), Times.Never);
        }

        [Fact]
        public async Task SkipOutboxHeader_IsNotPresent_RaiseOutboxNotification()
        {
            // arrange
            var message = new object();

            _mockSerializer.Setup(x => x.Serialize(typeof(object), message)).Verifiable();
            _mockOutboxRepository.Setup(x => x.Save(It.IsAny<OutboxMessage<Guid>>(), It.IsAny<CancellationToken>())).Verifiable();
            _mockOutboxNotificationService.Setup(x => x.Notify()).Verifiable();

            // act
            var target = new OutboxForwardingPublishInterceptor<object, Guid>(_mockLogger.Object, _mockOutboxRepository.Object, _mockInstanceIdProvider.Object, _mockOutboxNotificationService.Object, _mockOutboxSettings.Object);
            await target.OnHandle(new object(), () => Task.CompletedTask, _mockProducerContext.Object);
            target.Dispose();

            // assert
            _mockOutboxRepository.Verify(x => x.Save(It.IsAny<OutboxMessage<Guid>>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockOutboxNotificationService.Verify(x => x.Notify(), Times.Once);
        }
    }
}
