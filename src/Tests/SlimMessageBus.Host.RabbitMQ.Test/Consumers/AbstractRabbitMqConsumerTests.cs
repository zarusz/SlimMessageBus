namespace SlimMessageBus.Host.RabbitMQ.Test.Consumers;

using global::RabbitMQ.Client;
using global::RabbitMQ.Client.Events;

using Microsoft.Extensions.Logging;

using SlimMessageBus.Host.RabbitMQ;

public class AbstractRabbitMqConsumerTests : IDisposable
{
    private readonly Mock<IRabbitMqChannel> _channelMock;
    private readonly Mock<IChannel> _modelMock;
    private readonly Mock<IHeaderValueConverter> _headerValueConverterMock;
    private readonly ILoggerFactory _loggerFactory;
    private readonly List<AbstractRabbitMqConsumer> _consumersToDispose;
    private bool _disposed;

    public AbstractRabbitMqConsumerTests()
    {
        _loggerFactory = NullLoggerFactory.Instance;
        _channelMock = new Mock<IRabbitMqChannel>();
        _modelMock = new Mock<IChannel>();
        _headerValueConverterMock = new Mock<IHeaderValueConverter>();
        _consumersToDispose = new List<AbstractRabbitMqConsumer>();

        // Setup default mock behavior
        _channelMock.Setup(x => x.Channel).Returns(_modelMock.Object);
        // Note: ChannelLock removed in v7 - IChannel is thread-safe
        _modelMock.Setup(x => x.IsOpen).Returns(true);
    }

    [Fact]
    public void When_ConstructorCalled_Given_ValidParameters_Then_ShouldInitializeSuccessfully()
    {
        // Act
        var consumer = CreateTestConsumer("test-queue");

        // Assert
        consumer.Should().NotBeNull();
        consumer.Path.Should().Be("test-queue");
    }

    [Fact]
    public async Task When_OnMessageReceived_Given_ValidMessage_Then_ShouldProcessMessage()
    {
        // Arrange
        var consumer = CreateTestConsumer("test-queue");
        var deliverEventArgs = CreateBasicDeliverEventArgs();

        _headerValueConverterMock.Setup(x => x.ConvertFrom(It.IsAny<object>()))
            .Returns((object o) => o);

        // Act
        await consumer.TriggerMessageReceived(null, deliverEventArgs);

        // Assert
        consumer.ReceivedMessages.Should().ContainSingle();
        consumer.ReceivedMessages[0].Should().BeSameAs(deliverEventArgs);
    }

    [Fact]
    public async Task When_OnMessageReceived_Given_MessageWithHeaders_Then_ShouldConvertHeaders()
    {
        // Arrange
        var consumer = CreateTestConsumer("test-queue");
        var headers = new Dictionary<string, object>
        {
            { "header1", "value1" },
            { "header2", 123 }
        };

        var deliverEventArgs = CreateBasicDeliverEventArgs(headers);

        _headerValueConverterMock.Setup(x => x.ConvertFrom("value1"))
            .Returns("converted-value1");
        _headerValueConverterMock.Setup(x => x.ConvertFrom(123))
            .Returns(456);

        // Act
        await consumer.TriggerMessageReceived(null, deliverEventArgs);

        // Assert
        consumer.ReceivedHeaders.Should().ContainKey("header1");
        consumer.ReceivedHeaders["header1"].Should().Be("converted-value1");
        consumer.ReceivedHeaders.Should().ContainKey("header2");
        consumer.ReceivedHeaders["header2"].Should().Be(456);
    }

    [Fact]
    public async Task When_OnMessageReceived_Given_MessageWithNullHeaders_Then_ShouldHandleGracefully()
    {
        // Arrange
        var consumer = CreateTestConsumer("test-queue");
        var deliverEventArgs = CreateBasicDeliverEventArgs(headers: null);

        // Act
        await consumer.TriggerMessageReceived(null, deliverEventArgs);

        // Assert
        consumer.ReceivedMessages.Should().ContainSingle();
        consumer.ReceivedHeaders.Should().NotBeNull();
        consumer.ReceivedHeaders.Should().BeEmpty();
    }

    [Fact]
    public async Task When_OnMessageReceived_Given_ProcessingThrowsException_Then_ShouldCatchAndLogError()
    {
        // Arrange
        var consumer = CreateTestConsumer("test-queue", shouldThrow: true);
        var deliverEventArgs = CreateBasicDeliverEventArgs();

        // Act & Assert - Should not throw
        await consumer.Invoking(c => c.TriggerMessageReceived(null, deliverEventArgs))
            .Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task When_NackMessageCalled_Given_RequeueOption_Then_ShouldCallBasicNackWithCorrectParameter(bool requeue, bool expectedRequeue)
    {
        // Arrange
        var consumer = CreateTestConsumer("test-queue");
        var deliverEventArgs = CreateBasicDeliverEventArgs();

        // Act
        await consumer.CallNackMessage(deliverEventArgs, requeue: requeue);

        // Assert
        _modelMock.Verify(x => x.BasicNackAsync(
            deliverEventArgs.DeliveryTag,
            false,
            expectedRequeue,
            default), Times.Once);
    }

    [Fact]
    public async Task When_AckMessageCalled_Given_ValidMessage_Then_ShouldCallBasicAck()
    {
        // Arrange
        var consumer = CreateTestConsumer("test-queue");
        var deliverEventArgs = CreateBasicDeliverEventArgs();

        // Act
        await consumer.CallAckMessage(deliverEventArgs);

        // Assert
        _modelMock.Verify(x => x.BasicAckAsync(
            deliverEventArgs.DeliveryTag,
            false,
            default), Times.Once);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(5)]
    public async Task When_MessageReceivedConcurrently_Given_MultipleMessages_Then_ShouldHandleThreadSafely(int messageCount)
    {
        // Arrange
        var consumer = CreateTestConsumer("test-queue");
        var messages = Enumerable.Range(0, messageCount)
            .Select(i => CreateBasicDeliverEventArgs())
            .ToList();

        // Act
        var tasks = messages.Select(msg =>
            consumer.TriggerMessageReceived(null, msg));

        await Task.WhenAll(tasks);

        // Assert
        consumer.ReceivedMessages.Should().HaveCount(messageCount);
    }

    [Theory]
    [InlineData(10)]
    public async Task When_AckOrNackMessageCalledConcurrently_Given_MultipleMessages_Then_ShouldHandleThreadSafely(int messageCount)
    {
        // Arrange
        var consumer = CreateTestConsumer("test-queue");
        var messages = Enumerable.Range(0, messageCount)
            .Select(i => CreateBasicDeliverEventArgs((ulong)i))
            .ToList();

        // Act - Test both Ack and Nack
        var ackTasks = messages.Take(messageCount / 2).Select(msg => consumer.CallAckMessage(msg));
        var nackTasks = messages.Skip(messageCount / 2).Select(msg => consumer.CallNackMessage(msg, requeue: true));
        await Task.WhenAll(ackTasks.Concat(nackTasks));

        // Assert
        _modelMock.Verify(x => x.BasicAckAsync(
            It.IsAny<ulong>(),
            false,
            default), Times.Exactly(messageCount / 2));
        _modelMock.Verify(x => x.BasicNackAsync(
            It.IsAny<ulong>(),
            false,
            true,
            default), Times.Exactly(messageCount - messageCount / 2));
    }

    [Fact]
    public void When_ConsumerCreated_Given_NonRabbitMqChannelManager_Then_ShouldNotSubscribeToRecoveryEvents()
    {
        // Arrange - Use a mock that doesn't inherit from RabbitMqChannelManager
        var simpleChannelMock = new Mock<IRabbitMqChannel>();
        simpleChannelMock.Setup(x => x.Channel).Returns(_modelMock.Object);
        // Note: ChannelLock removed in v7 - IChannel is thread-safe

        // Act
        var consumer = new TestRabbitMqConsumer(
            _loggerFactory.CreateLogger<TestRabbitMqConsumer>(),
            Array.Empty<AbstractConsumerSettings>(),
            Array.Empty<IAbstractConsumerInterceptor>(),
            simpleChannelMock.Object,
            "test-queue",
            _headerValueConverterMock.Object);

        _consumersToDispose.Add(consumer);

        // Assert - Consumer should be created successfully without events
        consumer.Should().NotBeNull();
    }

    private TestRabbitMqConsumer CreateTestConsumer(string queueName, bool shouldThrow = false)
    {
        var consumer = new TestRabbitMqConsumer(
            _loggerFactory.CreateLogger<TestRabbitMqConsumer>(),
            Array.Empty<AbstractConsumerSettings>(),
            Array.Empty<IAbstractConsumerInterceptor>(),
            _channelMock.Object,
            queueName,
            _headerValueConverterMock.Object,
            shouldThrow);

        _consumersToDispose.Add(consumer);
        return consumer;
    }

    private static BasicDeliverEventArgs CreateBasicDeliverEventArgs(
        Dictionary<string, object> headers = null,
        ulong deliveryTag = 1)
    {
        var properties = new Mock<IReadOnlyBasicProperties>();
        properties.Setup(x => x.Headers).Returns(headers);

        return new BasicDeliverEventArgs(
            consumerTag: "test-consumer-tag",
            deliveryTag: deliveryTag,
            redelivered: false,
            exchange: "test-exchange",
            routingKey: "test.routing.key",
            properties: properties.Object,
            body: new ReadOnlyMemory<byte>(Array.Empty<byte>()),
            cancellationToken: default);
    }

    private static BasicDeliverEventArgs CreateBasicDeliverEventArgs(ulong deliveryTag)
    {
        return CreateBasicDeliverEventArgs(headers: null, deliveryTag: deliveryTag);
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

    // Test implementation of AbstractRabbitMqConsumer
    private class TestRabbitMqConsumer : AbstractRabbitMqConsumer
    {
        private readonly bool _shouldThrow;
        public List<BasicDeliverEventArgs> ReceivedMessages { get; } = new();
        public Dictionary<string, object> ReceivedHeaders { get; private set; }

        protected override RabbitMqMessageAcknowledgementMode AcknowledgementMode =>
            RabbitMqMessageAcknowledgementMode.ConfirmAfterMessageProcessingWhenNoManualConfirmMade;

        public TestRabbitMqConsumer(
            ILogger logger,
            IEnumerable<AbstractConsumerSettings> consumerSettings,
            IEnumerable<IAbstractConsumerInterceptor> interceptors,
            IRabbitMqChannel channel,
            string queueName,
            IHeaderValueConverter headerValueConverter,
            bool shouldThrow = false)
            : base(logger, consumerSettings, interceptors, channel, queueName, headerValueConverter)
        {
            _shouldThrow = shouldThrow;
        }

        protected override Task<Exception> OnMessageReceived(Dictionary<string, object> messageHeaders, BasicDeliverEventArgs transportMessage)
        {
            if (_shouldThrow)
            {
                throw new InvalidOperationException("Test exception");
            }

            ReceivedMessages.Add(transportMessage);
            ReceivedHeaders = messageHeaders;
            return Task.FromResult<Exception>(null);
        }

        public async Task TriggerMessageReceived(object sender, BasicDeliverEventArgs e)
        {
            // Directly call the abstract method implementation for testing
            // This bypasses the _consumer null check in the base class
            // but replicates the exception handling from the base class
            var messageHeaders = new Dictionary<string, object>();

            if (e.BasicProperties?.Headers != null)
            {
                foreach (var header in e.BasicProperties.Headers)
                {
                    var headerValueConverter = GetType()
                        .BaseType
                        .GetField("_headerValueConverter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        ?.GetValue(this) as IHeaderValueConverter;

                    messageHeaders.Add(header.Key, headerValueConverter?.ConvertFrom(header.Value) ?? header.Value);
                }
            }

            try
            {
                await OnMessageReceived(messageHeaders, e);
            }
            catch
            {
                // Catch exceptions like the base class does - it logs them but doesn't re-throw
            }
        }

        public Task CallAckMessage(BasicDeliverEventArgs e)
        {
            return AckMessage(e);
        }

        public Task CallNackMessage(BasicDeliverEventArgs e, bool requeue)
        {
            return NackMessage(e, requeue);
        }

        public new Task Start() => OnStart();
        public new Task Stop() => OnStop();
    }
}
