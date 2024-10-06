namespace SlimMessageBus.Host.Outbox.Test.Services;

using static SlimMessageBus.Host.Outbox.Services.OutboxSendingTask;

public sealed class OutboxSendingTaskTests
{
    public class DispatchBatchTests
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly Mock<IOutboxMessageRepository> _outboxRepositoryMock;
        private readonly Mock<IMessageBusBulkProducer> _producerMock;
        private readonly Mock<IMessageBusTarget> _messageBusTargetMock;
        private readonly OutboxSettings _outboxSettings;
        private readonly IServiceProvider _serviceProvider;
        private readonly OutboxSendingTask _sut;

        public DispatchBatchTests()
        {
            _outboxRepositoryMock = new Mock<IOutboxMessageRepository>();
            _producerMock = new Mock<IMessageBusBulkProducer>();
            _messageBusTargetMock = new Mock<IMessageBusTarget>();
            _outboxSettings = new OutboxSettings { MaxDeliveryAttempts = 5 };
            _serviceProvider = Mock.Of<IServiceProvider>();
            _loggerFactory = new NullLoggerFactory();

            _sut = new OutboxSendingTask(_loggerFactory, _outboxSettings, new CurrentTimeProvider(), _serviceProvider);
        }

        [Fact]
        public async Task DispatchBatch_ShouldReturnSuccess_WhenAllMessagesArePublished()
        {
            var batch = new List<OutboxBulkMessage>
            {
                new(Guid.NewGuid(), "Message1", typeof(string), new Dictionary<string, object>()),
                new(Guid.NewGuid(), "Message2", typeof(string), new Dictionary<string, object>())
            }.AsReadOnly();

            var results = new ProduceToTransportBulkResult<OutboxBulkMessage>(batch, null);

            _producerMock.Setup(x => x.ProduceToTransportBulk(batch, It.IsAny<string>(), It.IsAny<IMessageBusTarget>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(results);

            var (success, published) = await _sut.DispatchBatch(_outboxRepositoryMock.Object, _producerMock.Object, _messageBusTargetMock.Object, batch, "busName", "path", CancellationToken.None);

            success.Should().BeTrue();
            published.Should().Be(batch.Count);
        }

        [Fact]
        public async Task DispatchBatch_ShouldReturnFailure_WhenNotAllMessagesArePublished()
        {
            var batch = new List<OutboxBulkMessage>
            {
                new(Guid.NewGuid(), "Message1", typeof(string), new Dictionary<string, object>()),
                new(Guid.NewGuid(), "Message2", typeof(string), new Dictionary<string, object>())
            }.AsReadOnly();

            var results = new ProduceToTransportBulkResult<OutboxBulkMessage>([batch.First()], null);

            _producerMock.Setup(x => x.ProduceToTransportBulk(batch, It.IsAny<string>(), It.IsAny<IMessageBusTarget>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(results);

            var (success, published) = await _sut.DispatchBatch(_outboxRepositoryMock.Object, _producerMock.Object, _messageBusTargetMock.Object, batch, "busName", "path", CancellationToken.None);

            success.Should().BeFalse();
            published.Should().Be(1);
        }

        [Fact]
        public async Task DispatchBatch_ShouldIncrementDeliveryAttempts_WhenNotAllMessagesArePublished()
        {
            var batch = new List<OutboxBulkMessage>
            {
                new(Guid.NewGuid(), "Message1", typeof(string), new Dictionary<string, object>()),
                new(Guid.NewGuid(), "Message2", typeof(string), new Dictionary<string, object>())
            }.AsReadOnly();

            var results = new ProduceToTransportBulkResult<OutboxBulkMessage>([batch.First()], null);

            _producerMock.Setup(x => x.ProduceToTransportBulk(batch, It.IsAny<string>(), It.IsAny<IMessageBusTarget>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(results);

            await _sut.DispatchBatch(_outboxRepositoryMock.Object, _producerMock.Object, _messageBusTargetMock.Object, batch, "busName", "path", CancellationToken.None);

            _outboxRepositoryMock.Verify(x => x.IncrementDeliveryAttempt(It.Is<HashSet<Guid>>(ids => ids.Contains(batch[1].Id)), _outboxSettings.MaxDeliveryAttempts, CancellationToken.None), Times.Once);
        }
    }

    public class ProcessMessagesTests
    {
        private readonly Mock<IOutboxMessageRepository> _mockOutboxRepository;
        private readonly Mock<ICompositeMessageBus> _mockCompositeMessageBus;
        private readonly Mock<IMessageBusTarget> _mockMessageBusTarget;
        private readonly Mock<IMasterMessageBus> _mockMasterMessageBus;
        private readonly Mock<IMessageBusBulkProducer> _mockMessageBusBulkProducer;
        private readonly OutboxSettings _outboxSettings;
        private readonly OutboxSendingTask _sut;

        public ProcessMessagesTests()
        {
            _mockOutboxRepository = new Mock<IOutboxMessageRepository>();
            _mockCompositeMessageBus = new Mock<ICompositeMessageBus>();
            _mockMessageBusTarget = new Mock<IMessageBusTarget>();
            _mockMasterMessageBus = new Mock<IMasterMessageBus>();
            _mockMessageBusBulkProducer = _mockMasterMessageBus.As<IMessageBusBulkProducer>();

            _outboxSettings = new OutboxSettings
            {
                PollBatchSize = 50,
                MessageTypeResolver = new Mock<IMessageTypeResolver>().Object
            };

            _sut = new OutboxSendingTask(NullLoggerFactory.Instance, _outboxSettings, new CurrentTimeProvider(), null);
        }

        [Fact]
        public async Task ProcessMessages_ShouldReturnCorrectValues_WhenOutboxMessagesProcessedSuccessfully()
        {
            // Arrange
            var outboxMessages = CreateOutboxMessages(30);
            var cancellationToken = CancellationToken.None;

            _mockCompositeMessageBus.Setup(x => x.GetChildBus(It.IsAny<string>())).Returns(_mockMasterMessageBus.Object);
            _mockMessageBusBulkProducer.Setup(x => x.MaxMessagesPerTransaction).Returns(10);
            _mockMasterMessageBus.Setup(x => x.Serializer).Returns(new Mock<IMessageSerializer>().Object);

            _mockMessageBusBulkProducer.Setup(x => x.ProduceToTransportBulk(It.IsAny<IReadOnlyCollection<OutboxBulkMessage>>(), It.IsAny<string>(), It.IsAny<IMessageBusTarget>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<OutboxBulkMessage> envelopes, string path, IMessageBusTarget targetBus, CancellationToken cancellationToken) => new ProduceToTransportBulkResult<OutboxBulkMessage>(envelopes, null));

            var mockMessageTypeResolver = new Mock<IMessageTypeResolver>();
            mockMessageTypeResolver.Setup(x => x.ToType(It.IsAny<string>())).Returns(typeof(object));
            _outboxSettings.MessageTypeResolver = mockMessageTypeResolver.Object;

            // Act
            var result = await _sut.ProcessMessages(_mockOutboxRepository.Object, outboxMessages, _mockCompositeMessageBus.Object, _mockMessageBusTarget.Object, cancellationToken);

            // Assert
            result.RunAgain.Should().BeFalse();
            result.Count.Should().Be(30);
            _mockOutboxRepository.Verify(x => x.UpdateToSent(It.IsAny<HashSet<Guid>>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        }

        [Fact]
        public async Task ProcessMessages_ShouldReturnRunAgainTrue_WhenOutboxMessagesCountEqualsPollBatchSize()
        {
            // Arrange
            var outboxMessages = CreateOutboxMessages(50);
            var cancellationToken = CancellationToken.None;

            _mockCompositeMessageBus.Setup(x => x.GetChildBus(It.IsAny<string>())).Returns(_mockMasterMessageBus.Object);
            _mockMessageBusBulkProducer.Setup(x => x.MaxMessagesPerTransaction).Returns(10);
            _mockMasterMessageBus.Setup(x => x.Serializer).Returns(new Mock<IMessageSerializer>().Object);

            _mockMessageBusBulkProducer.Setup(x => x.ProduceToTransportBulk(It.IsAny<IReadOnlyCollection<OutboxBulkMessage>>(), It.IsAny<string>(), It.IsAny<IMessageBusTarget>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<OutboxBulkMessage> envelopes, string path, IMessageBusTarget targetBus, CancellationToken cancellationToken) => new ProduceToTransportBulkResult<OutboxBulkMessage>(envelopes, null));

            var mockMessageTypeResolver = new Mock<IMessageTypeResolver>();
            mockMessageTypeResolver.Setup(x => x.ToType(It.IsAny<string>())).Returns(typeof(object));
            _outboxSettings.MessageTypeResolver = mockMessageTypeResolver.Object;

            // Act
            var result = await _sut.ProcessMessages(_mockOutboxRepository.Object, outboxMessages, _mockCompositeMessageBus.Object, _mockMessageBusTarget.Object, cancellationToken);

            // Assert
            result.RunAgain.Should().BeTrue();
            result.Count.Should().Be(50);
        }

        [Fact]
        public async Task ProcessMessages_ShouldAbortDelivery_WhenBusIsNotRecognised()
        {
            // Arrange
            const int MessageCount = 10;

            var outboxMessages = CreateOutboxMessages(MessageCount);
            outboxMessages[0].BusName = null;
            outboxMessages[7].BusName = null;

            var knownBusCount = outboxMessages.Count(x => x.BusName != null);

            _mockMessageBusTarget.SetupGet(x => x.Target).Returns((IMessageBusProducer)null);

            _mockCompositeMessageBus.Setup(x => x.GetChildBus(It.IsAny<string>())).Returns(_mockMasterMessageBus.Object);
            _mockMessageBusBulkProducer.Setup(x => x.MaxMessagesPerTransaction).Returns(10);
            _mockMasterMessageBus.Setup(x => x.Serializer).Returns(new Mock<IMessageSerializer>().Object);

            _mockMessageBusBulkProducer.Setup(x => x.ProduceToTransportBulk(It.IsAny<IReadOnlyCollection<OutboxBulkMessage>>(), It.IsAny<string>(), It.IsAny<IMessageBusTarget>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<OutboxBulkMessage> envelopes, string path, IMessageBusTarget targetBus, CancellationToken cancellationToken) => new ProduceToTransportBulkResult<OutboxBulkMessage>(envelopes, null));

            var mockMessageTypeResolver = new Mock<IMessageTypeResolver>();
            mockMessageTypeResolver.Setup(x => x.ToType(It.IsAny<string>())).Returns((string type) => typeof(object));
            _outboxSettings.MessageTypeResolver = mockMessageTypeResolver.Object;

            // Act
            var result = await _sut.ProcessMessages(_mockOutboxRepository.Object, outboxMessages, _mockCompositeMessageBus.Object, _mockMessageBusTarget.Object, CancellationToken.None);

            // Assert
            _mockOutboxRepository.Verify(x => x.AbortDelivery(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockOutboxRepository.Verify(x => x.UpdateToSent(It.IsAny<HashSet<Guid>>(), It.IsAny<CancellationToken>()), Times.Once);
            result.RunAgain.Should().BeFalse();
            result.Count.Should().Be(knownBusCount);
        }

        [Fact]
        public async Task ProcessMessages_ShouldAbortDelivery_WhenMessageTypeIsNotRecognized()
        {
            // Arrange
            const string UnknownMessageType = "Unknown";
            const int MessageCount = 10;

            var outboxMessages = CreateOutboxMessages(MessageCount);
            outboxMessages[0].MessageType = UnknownMessageType;
            outboxMessages[7].MessageType = UnknownMessageType;

            var knownMessageCount = outboxMessages.Count(x => !x.MessageType.Equals(UnknownMessageType));

            _mockCompositeMessageBus.Setup(x => x.GetChildBus(It.IsAny<string>())).Returns(_mockMasterMessageBus.Object);
            _mockMessageBusBulkProducer.Setup(x => x.MaxMessagesPerTransaction).Returns(10);
            _mockMasterMessageBus.Setup(x => x.Serializer).Returns(new Mock<IMessageSerializer>().Object);

            _mockMessageBusBulkProducer.Setup(x => x.ProduceToTransportBulk(It.IsAny<IReadOnlyCollection<OutboxBulkMessage>>(), It.IsAny<string>(), It.IsAny<IMessageBusTarget>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<OutboxBulkMessage> envelopes, string path, IMessageBusTarget targetBus, CancellationToken cancellationToken) => new ProduceToTransportBulkResult<OutboxBulkMessage>(envelopes, null));

            var mockMessageTypeResolver = new Mock<IMessageTypeResolver>();
            mockMessageTypeResolver.Setup(x => x.ToType(It.IsAny<string>())).Returns((string type) => type == UnknownMessageType ? (Type)null : typeof(object));
            _outboxSettings.MessageTypeResolver = mockMessageTypeResolver.Object;

            // Act
            var result = await _sut.ProcessMessages(_mockOutboxRepository.Object, outboxMessages, _mockCompositeMessageBus.Object, _mockMessageBusTarget.Object, CancellationToken.None);

            // Assert
            _mockOutboxRepository.Verify(x => x.AbortDelivery(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockOutboxRepository.Verify(x => x.UpdateToSent(It.IsAny<HashSet<Guid>>(), It.IsAny<CancellationToken>()), Times.Once);
            result.RunAgain.Should().BeFalse();
            result.Count.Should().Be(knownMessageCount);
        }

        private static List<OutboxMessage> CreateOutboxMessages(int count)
        {
            return Enumerable
                .Range(0, count)
                .Select(
                    _ => new OutboxMessage
                    {
                        Id = Guid.NewGuid(),
                        MessageType = "TestType",
                        MessagePayload = [],
                        BusName = "TestBus",
                        Path = "TestPath"
                    })
                .ToList();
        }
    }
}