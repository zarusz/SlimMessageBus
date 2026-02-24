namespace SlimMessageBus.Host.RabbitMQ.Test.Consumers;

using global::RabbitMQ.Client;
using global::RabbitMQ.Client.Events;

using SlimMessageBus.Host.RabbitMQ;

public class AbstractRabbitMqConsumer_CircuitBreakerTests : IDisposable
{
    private readonly Mock<IRabbitMqChannel> _channelMock;
    private readonly Mock<IModel> _modelMock;
    private readonly Mock<IHeaderValueConverter> _headerValueConverterMock;
    private readonly ILoggerFactory _loggerFactory;
    private readonly List<TestRabbitMqConsumer> _consumersToDispose;
    private bool _disposed;

    public AbstractRabbitMqConsumer_CircuitBreakerTests()
    {
        _loggerFactory = NullLoggerFactory.Instance;
        _channelMock = new Mock<IRabbitMqChannel>();
        _modelMock = new Mock<IModel>();
        _headerValueConverterMock = new Mock<IHeaderValueConverter>();
        _consumersToDispose = [];

        // Setup default mock behavior
        _channelMock.Setup(x => x.Channel).Returns(_modelMock.Object);
        _channelMock.Setup(x => x.ChannelLock).Returns(new object());
        _modelMock.Setup(x => x.IsOpen).Returns(true);
        _modelMock.Setup(x => x.BasicConsume(
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<IBasicConsumer>()))
            .Returns("consumer-tag-123");
    }

    [Fact]
    public async Task When_OnStart_Then_ShouldSetTransportStartedFlag()
    {
        // Arrange
        var consumer = CreateTestConsumer("test-queue");

        // Act
        await consumer.TestOnStart();

        // Assert
        consumer.GetTransportStarted().Should().BeTrue("_transportStarted should be set to true after OnStart");
    }

    [Fact]
    public async Task When_OnStop_Then_ShouldClearTransportStartedFlag()
    {
        // Arrange
        var consumer = CreateTestConsumer("test-queue");
        await consumer.TestOnStart();

        // Act
        await consumer.TestOnStop();

        // Assert
        consumer.GetTransportStarted().Should().BeFalse("_transportStarted should be set to false after OnStop");
    }

    [Fact]
    public async Task When_OnStart_Then_ShouldRegisterConsumerWithRabbitMq()
    {
        // Arrange
        var consumer = CreateTestConsumer("test-queue");

        // Act
        await consumer.TestOnStart();

        // Assert
        _modelMock.Verify(x => x.BasicConsume(
            "test-queue",
            It.IsAny<bool>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<IBasicConsumer>()), Times.Once);
    }

    [Fact]
    public async Task When_OnStop_Then_ShouldCancelConsumerWithRabbitMq()
    {
        // Arrange
        var consumer = CreateTestConsumer("test-queue");
        await consumer.TestOnStart();

        // Act
        await consumer.TestOnStop();

        // Assert
        _modelMock.Verify(x => x.BasicCancel("consumer-tag-123"), Times.Once);
    }

    [Fact]
    public async Task When_StartStopStart_Then_ShouldProperlyToggleTransportStartedFlag()
    {
        // Arrange
        var consumer = CreateTestConsumer("test-queue");

        // Act & Assert
        consumer.GetTransportStarted().Should().BeFalse("Initial state should be false");

        await consumer.TestOnStart();
        consumer.GetTransportStarted().Should().BeTrue("After first Start");

        await consumer.TestOnStop();
        consumer.GetTransportStarted().Should().BeFalse("After Stop");

        await consumer.TestOnStart();
        consumer.GetTransportStarted().Should().BeTrue("After second Start");
    }

    private TestRabbitMqConsumer CreateTestConsumer(string queueName)
    {
        var consumer = new TestRabbitMqConsumer(
            _loggerFactory.CreateLogger<TestRabbitMqConsumer>(),
            Array.Empty<AbstractConsumerSettings>(),
            Array.Empty<IAbstractConsumerInterceptor>(),
            _channelMock.Object,
            queueName,
            _headerValueConverterMock.Object);

        _consumersToDispose.Add(consumer);
        return consumer;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var consumer in _consumersToDispose)
            {
                consumer?.DisposeAsync().AsTask().Wait();
            }
            _consumersToDispose.Clear();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    // Test implementation that exposes internal state
    private class TestRabbitMqConsumer : AbstractRabbitMqConsumer
    {
        protected override RabbitMqMessageAcknowledgementMode AcknowledgementMode =>
            RabbitMqMessageAcknowledgementMode.ConfirmAfterMessageProcessingWhenNoManualConfirmMade;

        public TestRabbitMqConsumer(
            ILogger logger,
            IEnumerable<AbstractConsumerSettings> consumerSettings,
            IEnumerable<IAbstractConsumerInterceptor> interceptors,
            IRabbitMqChannel channel,
            string queueName,
            IHeaderValueConverter headerValueConverter)
            : base(logger, consumerSettings, interceptors, channel, queueName, headerValueConverter)
        {
        }

        protected override Task<Exception> OnMessageReceived(Dictionary<string, object> messageHeaders, BasicDeliverEventArgs transportMessage)
        {
            return Task.FromResult<Exception>(null);
        }

        public Task TestOnStart() => OnStart();
        public Task TestOnStop() => OnStop();

        public bool GetTransportStarted()
        {
            var field = typeof(AbstractRabbitMqConsumer).GetField("_transportStarted", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (bool)field!.GetValue(this)!;
        }
    }
}
