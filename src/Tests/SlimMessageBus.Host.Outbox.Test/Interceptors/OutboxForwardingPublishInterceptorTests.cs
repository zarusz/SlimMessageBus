namespace SlimMessageBus.Host.Outbox.Test.Interceptors;

using SlimMessageBus.Host.Outbox;
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
            var mockOutboxRepository = new Mock<IOutboxMessageRepository<OutboxMessage>>();
            var mockOutboxNotificationService = new Mock<IOutboxNotificationService>();
            var mockOutboxSettings = new Mock<OutboxSettings>();
            var mockOutboxMessageFactory = new Mock<IOutboxMessageFactory>();

            // act
            var target = new OutboxForwardingPublishInterceptor<object>(mockLogger.Object, mockOutboxMessageFactory.Object, mockOutboxNotificationService.Object, mockOutboxSettings.Object);
            var actual = target.Order;

            // assert
            actual.Should().Be(expected);
        }

        [Fact]
        public void OutboxForwardingPublisher_MustImplement_IInterceptorWithOrder()
        {
            // act
            var actual = typeof(OutboxForwardingPublishInterceptor<object>).IsAssignableTo(typeof(IInterceptorWithOrder));

            // assert
            actual.Should().BeTrue();
        }
    }

    public class OnHandleTests
    {
        private readonly Mock<ILogger<OutboxForwardingPublishInterceptor>> _mockLogger;
        private readonly Mock<IOutboxMessageRepository<OutboxMessage>> _mockOutboxRepository;
        private readonly Mock<IOutboxMessageFactory> _mockOutboxFactory;
        private readonly Mock<IMessageSerializer> _mockSerializer;
        private readonly Mock<IMessageSerializerProvider> _mockSerializerProvider;
        private readonly Mock<IMasterMessageBus> _mockMasterMessageBus;
        private readonly Mock<IOutboxNotificationService> _mockOutboxNotificationService;
        private readonly Mock<OutboxSettings> _mockOutboxSettings;
        private readonly Mock<IMessageBusTarget> _mockTargetBus;
        private readonly Mock<IProducerContext> _mockProducerContext;
        private readonly Mock<IOutboxMessageFactory> _mockOutboxMessageFactory;

        public OnHandleTests()
        {
            _mockLogger = new Mock<ILogger<OutboxForwardingPublishInterceptor>>();
            _mockOutboxRepository = new Mock<IOutboxMessageRepository<OutboxMessage>>();
            _mockOutboxFactory = new Mock<IOutboxMessageFactory>();
            _mockOutboxNotificationService = new Mock<IOutboxNotificationService>();
            _mockOutboxSettings = new Mock<OutboxSettings>();

            _mockSerializer = new Mock<IMessageSerializer>();

            _mockSerializerProvider = new Mock<IMessageSerializerProvider>();
            _mockSerializerProvider.Setup(x => x.GetSerializer(It.IsAny<string>())).Returns(_mockSerializer.Object).Verifiable();

            _mockMasterMessageBus = new Mock<IMasterMessageBus>();
            _mockMasterMessageBus.SetupGet(x => x.SerializerProvider).Returns(_mockSerializerProvider.Object).Verifiable();

            _mockTargetBus = new Mock<IMessageBusTarget>();
            _mockTargetBus.SetupGet(x => x.Target).Returns(_mockMasterMessageBus.Object);

            _mockProducerContext = new Mock<IProducerContext>();
            _mockProducerContext.SetupGet(x => x.Bus).Returns(_mockTargetBus.Object);

            _mockOutboxMessageFactory = new Mock<IOutboxMessageFactory>();
            _mockOutboxMessageFactory
                .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OutboxMessage());
        }

        [Fact]
        public async Task SkipOutboxHeader_IsPresent_PromoteToNext()
        {
            // arrange
            _mockProducerContext.SetupGet(x => x.Headers).Returns(new Dictionary<string, object> { { OutboxForwardingPublishInterceptor<object>.SkipOutboxHeader, true } });
            _mockOutboxFactory.Setup(x => x.Create(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>())).Verifiable();


            var nextCalled = 0;
            var next = () =>
            {
                nextCalled++;
                return Task.CompletedTask;
            };

            // act
            var target = new OutboxForwardingPublishInterceptor<object>(_mockLogger.Object, _mockOutboxMessageFactory.Object, _mockOutboxNotificationService.Object, _mockOutboxSettings.Object);
            await target.OnHandle(new object(), next, _mockProducerContext.Object);
            target.Dispose();

            // assert
            _mockProducerContext.VerifyGet(x => x.Bus, Times.AtLeastOnce);
            _mockProducerContext.VerifyGet(x => x.Headers, Times.AtLeastOnce);
            _mockOutboxFactory
                .Verify(x => x.Create(
                    It.IsAny<string>(),
                    It.IsAny<IDictionary<string, object>>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                    Times.Never);

            nextCalled.Should().Be(1);
        }

        [Fact]
        public async Task SkipOutboxHeader_IsNotPresent_DoNotPromoteToNext()
        {
            // arrange
            var message = new object();

            _mockSerializer.Setup(x => x.Serialize(typeof(object), message)).Verifiable();
            _mockOutboxFactory.Setup(x => x.Create(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>())).Verifiable();

            var nextCalled = 0;
            var next = () =>
            {
                nextCalled++;
                return Task.CompletedTask;
            };

            // act
            var target = new OutboxForwardingPublishInterceptor<object>(_mockLogger.Object, _mockOutboxMessageFactory.Object, _mockOutboxNotificationService.Object, _mockOutboxSettings.Object);
            await target.OnHandle(new object(), next, _mockProducerContext.Object);
            target.Dispose();

            // assert
            nextCalled.Should().Be(0);
            _mockOutboxFactory
                .Verify(x => x.Create(
                    It.IsAny<string>(),
                    It.IsAny<IDictionary<string, object>>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                    Times.Never);
        }

        [Fact]
        public async Task SkipOutboxHeader_IsPresent_DoNotRaiseOutboxNotification()
        {
            // arrange
            _mockProducerContext.SetupGet(x => x.Headers).Returns(new Dictionary<string, object> { { OutboxForwardingPublishInterceptor<object>.SkipOutboxHeader, true } });
            _mockOutboxFactory.Setup(x => x.Create(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>())).Verifiable();
            _mockOutboxNotificationService.Setup(x => x.Notify()).Verifiable();

            // act
            var target = new OutboxForwardingPublishInterceptor<object>(_mockLogger.Object, _mockOutboxMessageFactory.Object, _mockOutboxNotificationService.Object, _mockOutboxSettings.Object);
            await target.OnHandle(new object(), () => Task.CompletedTask, _mockProducerContext.Object);
            target.Dispose();

            // assert
            _mockOutboxFactory
                .Verify(x => x.Create(
                    It.IsAny<string>(),
                    It.IsAny<IDictionary<string, object>>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                    Times.Never);
            _mockOutboxNotificationService.Verify(x => x.Notify(), Times.Never);
        }

        [Fact]
        public async Task SkipOutboxHeader_IsNotPresent_RaiseOutboxNotification()
        {
            // arrange
            var message = new object();

            _mockSerializer.Setup(x => x.Serialize(typeof(object), message)).Verifiable();
            _mockOutboxFactory.Setup(x => x.Create(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>())).Verifiable();
            _mockOutboxNotificationService.Setup(x => x.Notify()).Verifiable();

            // act
            var target = new OutboxForwardingPublishInterceptor<object>(_mockLogger.Object, _mockOutboxMessageFactory.Object, _mockOutboxNotificationService.Object, _mockOutboxSettings.Object);
            await target.OnHandle(new object(), () => Task.CompletedTask, _mockProducerContext.Object);
            target.Dispose();

            // assert
            _mockOutboxFactory
                .Verify(x => x.Create(
                    It.IsAny<string>(),
                    It.IsAny<IDictionary<string, object>>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                    Times.Never);
            _mockOutboxNotificationService.Verify(x => x.Notify(), Times.Once);
        }
    }
}
