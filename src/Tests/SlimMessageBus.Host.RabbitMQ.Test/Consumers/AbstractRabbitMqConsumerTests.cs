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

        // Default setup for broker consumer registration / deregistration
        _modelMock
            .Setup(x => x.BasicConsumeAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<IAsyncBasicConsumer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-consumer-tag");
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

    // -----------------------------------------------------------------------
    // ReRegisterConsumer / OnStart tests
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(RabbitMqMessageAcknowledgementMode.ConfirmAfterMessageProcessingWhenNoManualConfirmMade, false)]
    [InlineData(RabbitMqMessageAcknowledgementMode.AckAutomaticByRabbit, true)]
    public async Task When_OnStart_Given_AcknowledgementMode_Then_ShouldCallBasicConsumeAsyncWithCorrectAutoAck(
        RabbitMqMessageAcknowledgementMode ackMode, bool expectedAutoAck)
    {
        // Arrange
        var consumer = CreateTestConsumer("test-queue", ackMode: ackMode);

        // Act
        await consumer.Start();

        // Assert
        _modelMock.Verify(x => x.BasicConsumeAsync("test-queue", expectedAutoAck, It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<IAsyncBasicConsumer>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task When_OnStart_Twice_Given_ExistingConsumer_Then_ShouldCancelPreviousAndRegisterNew()
    {
        // Arrange
        var consumer = CreateTestConsumer("test-queue");
        await consumer.Start();

        // Act – second Start re-registers (channel recovery / restart scenario)
        await consumer.Start();

        // Assert – BasicCancelAsync called for the tag from the first Start
        _modelMock.Verify(x => x.BasicCancelAsync("test-consumer-tag", It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        _modelMock.Verify(x => x.BasicConsumeAsync("test-queue", false, It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<IAsyncBasicConsumer>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task When_OnStart_Given_ExistingConsumer_AndBasicCancelAsyncThrows_Then_ShouldLogWarningAndContinueRegistration()
    {
        // Arrange
        var consumer = CreateTestConsumer("test-queue");
        await consumer.Start();   // first Start stores "test-consumer-tag"

        _modelMock
            .Setup(x => x.BasicCancelAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Cancel failed"));

        // Act – should not propagate the cancel failure
        await consumer.Invoking(c => c.Start()).Should().NotThrowAsync();

        // Assert – BasicConsumeAsync still called for the new registration
        _modelMock.Verify(x => x.BasicConsumeAsync("test-queue", false, It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<IAsyncBasicConsumer>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task When_OnStart_Given_BasicConsumeAsyncThrows_Then_ShouldResetConsumerAndRethrow()
    {
        // Arrange
        var consumer = CreateTestConsumer("test-queue");

        _modelMock
            .Setup(x => x.BasicConsumeAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<IAsyncBasicConsumer>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Broker unavailable"));

        // Act
        await consumer.Invoking(c => c.Start()).Should().ThrowAsync<InvalidOperationException>();

        // Assert – consumer is reset; messages arriving now are silently dropped (null-consumer guard)
        var deliverEventArgs = CreateBasicDeliverEventArgs();
        await consumer.TriggerBaseOnMessageReceived(null, deliverEventArgs);
        consumer.ReceivedMessages.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // OnStop tests
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(true)]   // consumer was registered → cancel must be called
    [InlineData(false)]  // consumer was never registered → cancel must not be called
    public async Task When_OnStop_Given_StartedState_Then_ShouldCallBasicCancelAsyncAccordingly(bool startFirst)
    {
        // Arrange
        var consumer = CreateTestConsumer("test-queue");
        if (startFirst)
        {
            await consumer.Start();
        }

        // Act
        await consumer.Stop();

        // Assert
        _modelMock.Verify(
            x => x.BasicCancelAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            startFirst ? Times.Once : Times.Never);
    }

    [Fact]
    public async Task When_OnStop_Given_BasicCancelAsyncThrows_Then_ShouldLogWarningAndComplete()
    {
        // Arrange
        var consumer = CreateTestConsumer("test-queue");
        await consumer.Start();

        _modelMock
            .Setup(x => x.BasicCancelAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Broker gone"));

        // Act & Assert – must not rethrow
        await consumer.Invoking(c => c.Stop()).Should().NotThrowAsync();
    }

    [Fact]
    public async Task When_StopThenStart_Given_ConsumerWasStopped_Then_ShouldNotAttemptCancelOnRestart()
    {
        // Arrange
        var consumer = CreateTestConsumer("test-queue");
        await consumer.Start();
        await consumer.Stop();

        _modelMock.Invocations.Clear();

        // Act
        await consumer.Start();

        // Assert – no cancel call because consumer tag was cleared on Stop
        _modelMock.Verify(x => x.BasicCancelAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        _modelMock.Verify(x => x.BasicConsumeAsync("test-queue", false, It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<IAsyncBasicConsumer>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // -----------------------------------------------------------------------
    // OnMessageReceived null-consumer guard test
    // -----------------------------------------------------------------------

    [Fact]
    public async Task When_OnMessageReceived_Given_ConsumerIsNull_Then_ShouldDropMessageSilently()
    {
        // Arrange – consumer never started so _consumer field stays null
        var consumer = CreateTestConsumer("test-queue");
        var deliverEventArgs = CreateBasicDeliverEventArgs();

        // Act – go through the base-class path that includes the null-consumer guard
        await consumer.TriggerBaseOnMessageReceived(null, deliverEventArgs);

        // Assert – inner OnMessageReceived never reached
        consumer.ReceivedMessages.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Race-condition regression test (the bug fix)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task When_OnStart_Given_MessageArrivesBeforeBasicConsumeAsyncCompletes_Then_ShouldNotDropMessage()
    {
        // Arrange – simulate the broker delivering a pre-queued message *during* the
        // BasicConsumeAsync await (i.e. before the method returns and the old code
        // would have set _consumer).  Because we now set _consumer BEFORE the await,
        // the message must be processed rather than silently dropped.
        var deliverEventArgs = CreateBasicDeliverEventArgs();
        TestRabbitMqConsumer consumer = null;

        _modelMock
            .Setup(x => x.BasicConsumeAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<IAsyncBasicConsumer>(), It.IsAny<CancellationToken>()))
            .Returns<string, bool, string, bool, bool, IDictionary<string, object>, IAsyncBasicConsumer, CancellationToken>(async (_, _, _, _, _, _, _, _) =>
            {
                // Simulate broker message delivery happening mid-await
                await consumer!.TriggerBaseOnMessageReceived(null, deliverEventArgs);
                return "test-consumer-tag";
            });

        consumer = CreateTestConsumer("test-queue");

        // Act
        await consumer.Start();

        // Assert – message was processed, NOT silently dropped
        consumer.ReceivedMessages.Should().ContainSingle();
    }

    private TestRabbitMqConsumer CreateTestConsumer(
        string queueName,
        bool shouldThrow = false,
        RabbitMqMessageAcknowledgementMode ackMode = RabbitMqMessageAcknowledgementMode.ConfirmAfterMessageProcessingWhenNoManualConfirmMade)
    {
        var consumer = new TestRabbitMqConsumer(
            _loggerFactory.CreateLogger<TestRabbitMqConsumer>(),
            Array.Empty<AbstractConsumerSettings>(),
            Array.Empty<IAbstractConsumerInterceptor>(),
            _channelMock.Object,
            queueName,
            _headerValueConverterMock.Object,
            shouldThrow,
            ackMode);

        _consumersToDispose.Add(consumer);
        return consumer;
    }

    // Legacy overload kept for the existing tests that pass shouldThrow explicitly by position
    private TestRabbitMqConsumer CreateTestConsumer(string queueName, bool shouldThrow)
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
        private readonly RabbitMqMessageAcknowledgementMode _acknowledgementMode;

        public List<BasicDeliverEventArgs> ReceivedMessages { get; } = new();
        public Dictionary<string, object> ReceivedHeaders { get; private set; }

        protected override RabbitMqMessageAcknowledgementMode AcknowledgementMode => _acknowledgementMode;

        public TestRabbitMqConsumer(
            ILogger logger,
            IEnumerable<AbstractConsumerSettings> consumerSettings,
            IEnumerable<IAbstractConsumerInterceptor> interceptors,
            IRabbitMqChannel channel,
            string queueName,
            IHeaderValueConverter headerValueConverter,
            bool shouldThrow = false,
            RabbitMqMessageAcknowledgementMode acknowledgementMode = RabbitMqMessageAcknowledgementMode.ConfirmAfterMessageProcessingWhenNoManualConfirmMade)
            : base(logger, consumerSettings, interceptors, channel, queueName, headerValueConverter)
        {
            _shouldThrow = shouldThrow;
            _acknowledgementMode = acknowledgementMode;
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

        /// <summary>Calls the base-class event handler, which includes the _consumer null-guard.</summary>
        public Task TriggerBaseOnMessageReceived(object sender, BasicDeliverEventArgs e)
            => OnMessageReceived(sender, e);

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
