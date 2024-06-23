namespace SlimMessageBus.Host.Outbox.Test.Interceptors;

using Microsoft.Extensions.Logging.Abstractions;

using static SlimMessageBus.Host.Outbox.OutboxSendingTask;

public class OutboxSendingTaskTests
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly Mock<IOutboxRepository> _outboxRepositoryMock;
    private readonly Mock<IMessageBusBulkProducer> _producerMock;
    private readonly Mock<IMessageBusTarget> _messageBusTargetMock;
    private readonly OutboxSettings _outboxSettings;
    private readonly IServiceProvider _serviceProvider;
    private readonly OutboxSendingTask _sut;

    public OutboxSendingTaskTests()
    {
        _outboxRepositoryMock = new Mock<IOutboxRepository>();
        _producerMock = new Mock<IMessageBusBulkProducer>();
        _messageBusTargetMock = new Mock<IMessageBusTarget>();
        _outboxSettings = new OutboxSettings { MaxDeliveryAttempts = 5 };
        _serviceProvider = Mock.Of<IServiceProvider>();
        _loggerFactory = new NullLoggerFactory();

        _sut = new OutboxSendingTask(_loggerFactory, _outboxSettings, _serviceProvider);
    }

    [Fact]
    public async Task DispatchBatchAsync_ShouldReturnSuccess_WhenAllMessagesArePublished()
    {
        var batch = new List<OutboxBulkMessage>
        {
            new(Guid.NewGuid(), "Message1", typeof(string), new Dictionary<string, object>()),
            new(Guid.NewGuid(), "Message2", typeof(string), new Dictionary<string, object>())
        }.AsReadOnly();

        var results = new ProduceToTransportBulkResult<OutboxBulkMessage>(batch, null);

        _producerMock.Setup(x => x.ProduceToTransportBulk(batch, It.IsAny<string>(), It.IsAny<IMessageBusTarget>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        var (success, published) = await _sut.DispatchBatchAsync(_outboxRepositoryMock.Object, _producerMock.Object, _messageBusTargetMock.Object, batch, "busName", "path", CancellationToken.None);

        success.Should().BeTrue();
        published.Should().Be(batch.Count);
    }

    [Fact]
    public async Task DispatchBatchAsync_ShouldReturnFailure_WhenNotAllMessagesArePublished()
    {
        var batch = new List<OutboxBulkMessage>
        {
            new(Guid.NewGuid(), "Message1", typeof(string), new Dictionary<string, object>()),
            new(Guid.NewGuid(), "Message2", typeof(string), new Dictionary<string, object>())
        }.AsReadOnly();

        var results = new ProduceToTransportBulkResult<OutboxBulkMessage>([batch.First()], null);

        _producerMock.Setup(x => x.ProduceToTransportBulk(batch, It.IsAny<string>(), It.IsAny<IMessageBusTarget>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        var (success, published) = await _sut.DispatchBatchAsync(_outboxRepositoryMock.Object, _producerMock.Object, _messageBusTargetMock.Object, batch, "busName", "path", CancellationToken.None);

        success.Should().BeFalse();
        published.Should().Be(1);
    }

    [Fact]
    public async Task DispatchBatchAsync_ShouldIncrementDeliveryAttempts_WhenNotAllMessagesArePublished()
    {
        var batch = new List<OutboxBulkMessage>
        {
            new(Guid.NewGuid(), "Message1", typeof(string), new Dictionary<string, object>()),
            new(Guid.NewGuid(), "Message2", typeof(string), new Dictionary<string, object>())
        }.AsReadOnly();

        var results = new ProduceToTransportBulkResult<OutboxBulkMessage>([batch.First()], null);

        _producerMock.Setup(x => x.ProduceToTransportBulk(batch, It.IsAny<string>(), It.IsAny<IMessageBusTarget>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        await _sut.DispatchBatchAsync(_outboxRepositoryMock.Object, _producerMock.Object, _messageBusTargetMock.Object, batch, "busName", "path", CancellationToken.None);

        _outboxRepositoryMock.Verify(x => x.IncrementDeliveryAttempt(It.Is<HashSet<Guid>>(ids => ids.Contains(batch[1].Id)), _outboxSettings.MaxDeliveryAttempts, CancellationToken.None), Times.Once);
    }
}
