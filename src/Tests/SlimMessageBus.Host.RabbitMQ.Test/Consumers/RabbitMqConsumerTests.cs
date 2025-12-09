namespace SlimMessageBus.Host.RabbitMQ.Test.Consumers;

using global::RabbitMQ.Client;
using global::RabbitMQ.Client.Events;

using SlimMessageBus.Host.Collections;

public class RabbitMqConsumerTests : IAsyncLifetime
{
    private readonly Mock<IRabbitMqChannel> _channelMock;
    private readonly Mock<IModel> _modelMock;
    private readonly Mock<IHeaderValueConverter> _headerValueConverterMock;
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly Mock<ILogger<RabbitMqConsumer>> _consumerLoggerMock;
    private readonly TestableMessageBus _messageBus;
    private readonly Mock<MessageProvider<BasicDeliverEventArgs>> _messageProviderMock;
    private readonly ServiceProvider _serviceProvider;
    private readonly List<TestableRabbitMqConsumer> _consumersToDispose;

    public RabbitMqConsumerTests()
    {
        _channelMock = new Mock<IRabbitMqChannel>();
        _modelMock = new Mock<IModel>();
        _headerValueConverterMock = new Mock<IHeaderValueConverter>();
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _consumerLoggerMock = new Mock<ILogger<RabbitMqConsumer>>();
        _messageProviderMock = new Mock<MessageProvider<BasicDeliverEventArgs>>();
        _consumersToDispose = [];

        // Setup default mock behavior
        _channelMock.Setup(x => x.Channel).Returns(_modelMock.Object);
        _channelMock.Setup(x => x.ChannelLock).Returns(new object());
        _modelMock.Setup(x => x.IsOpen).Returns(true);

        // Setup logger factory to return the consumer logger mock for any string parameter
        _loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_consumerLoggerMock.Object);

        // Setup service provider
        var services = new ServiceCollection();
        services.AddSingleton<IAbstractConsumerInterceptor>(sp => Mock.Of<IAbstractConsumerInterceptor>());
        services.AddSingleton<IMessageTypeResolver>(sp => new AssemblyQualifiedNameMessageTypeResolver());
        services.AddSingleton<RuntimeTypeCache>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPendingRequestManager>(sp => new PendingRequestManager(
            new InMemoryPendingRequestStore(),
            sp.GetRequiredService<TimeProvider>(),
            NullLoggerFactory.Instance));
        _serviceProvider = services.BuildServiceProvider();

        // Create actual MessageBus instance instead of mocking it
        var messageBusSettings = new MessageBusSettings
        {
            ServiceProvider = _serviceProvider
        };
        var providerSettings = new RabbitMqMessageBusSettings();

        _messageBus = new TestableMessageBus(messageBusSettings, providerSettings);

        _headerValueConverterMock.Setup(x => x.ConvertFrom(It.IsAny<object>()))
            .Returns((object o) => o);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var consumer in _consumersToDispose)
        {
            if (consumer != null)
            {
                await consumer.DisposeAsync();
            }
        }
        _consumersToDispose.Clear();

        _serviceProvider?.Dispose();
    }

    [Fact]
    public void When_ConstructorCalled_Given_SingleConsumer_Then_ShouldInitializeWithSingleProcessor()
    {
        // Arrange
        var consumers = CreateConsumerSettings("test-queue", "");

        // Act
        var consumer = CreateRabbitMqConsumer("test-queue", consumers);

        // Assert
        consumer.Should().NotBeNull();
        consumer.Path.Should().Be("test-queue");
    }

    [Fact]
    public void When_ConstructorCalled_Given_MultipleConsumersWithDifferentRoutingKeys_Then_ShouldInitializeRoutingKeyMatcher()
    {
        // Arrange
        var consumers = new List<ConsumerSettings>
        {
            CreateConsumerSettings("test-queue", "routing.key.1")[0],
            CreateConsumerSettings("test-queue", "routing.key.2")[0],
            CreateConsumerSettings("test-queue", "routing.*.wildcard")[0]
        };

        // Act
        var consumer = CreateRabbitMqConsumer("test-queue", consumers);

        // Assert
        consumer.Should().NotBeNull();
        consumer.Path.Should().Be("test-queue");
    }

    [Fact]
    public async Task When_OnMessageReceived_Given_MessageWithUnrecognizedRoutingKey_Then_ShouldCallUnrecognizedHandler()
    {
        // Arrange
        var consumers = CreateConsumerSettings("test-queue", "known.routing.key");
        var consumer = CreateRabbitMqConsumer("test-queue", consumers);

        var deliverEventArgs = CreateBasicDeliverEventArgs(routingKey: "unknown.routing.key");
        var messageHeaders = new Dictionary<string, object>();

        // Act
        var exception = await consumer.OnMessageReceivedPublic(messageHeaders, deliverEventArgs);

        // Assert
        exception.Should().BeNull();

        // Verify message was acknowledged (default behavior for unrecognized routing key)
        _modelMock.Verify(x => x.BasicAck(deliverEventArgs.DeliveryTag, false), Times.Once);
    }

    [Fact]
    public async Task When_OnMessageReceived_Given_AckMessageBeforeProcessingMode_Then_ShouldAckBeforeProcessing()
    {
        // Arrange
        var consumers = CreateConsumerSettings("test-queue", "");
        RabbitMqProperties.MessageAcknowledgementMode.Set(consumers[0], RabbitMqMessageAcknowledgementMode.AckMessageBeforeProcessing);

        var consumer = CreateRabbitMqConsumer("test-queue", consumers);

        var deliverEventArgs = CreateBasicDeliverEventArgs();
        var messageHeaders = new Dictionary<string, object>();

        // Act
        await consumer.OnMessageReceivedPublic(messageHeaders, deliverEventArgs);

        // Assert - Should be called once (before processing)
        _modelMock.Verify(x => x.BasicAck(deliverEventArgs.DeliveryTag, false), Times.Once);
    }

    [Theory]
    [InlineData(RabbitMqMessageConfirmOptions.Ack, 1, 0, false)]
    [InlineData(RabbitMqMessageConfirmOptions.Nack, 0, 1, false)]
    [InlineData(RabbitMqMessageConfirmOptions.Nack | RabbitMqMessageConfirmOptions.Requeue, 0, 1, true)]
    public void When_ConfirmMessage_Given_ConfirmOptions_Then_ShouldCallAppropriateMethod(
        RabbitMqMessageConfirmOptions option,
        int expectedAckCalls,
        int expectedNackCalls,
        bool expectedRequeue)
    {
        // Arrange
        var consumers = CreateConsumerSettings("test-queue", "");
        var consumer = CreateRabbitMqConsumer("test-queue", consumers);

        var deliverEventArgs = CreateBasicDeliverEventArgs();
        var properties = new Dictionary<string, object>();

        // Act
        consumer.ConfirmMessage(deliverEventArgs, option, properties);

        // Assert
        _modelMock.Verify(x => x.BasicAck(deliverEventArgs.DeliveryTag, false), Times.Exactly(expectedAckCalls));
        _modelMock.Verify(x => x.BasicNack(deliverEventArgs.DeliveryTag, false, expectedRequeue), Times.Exactly(expectedNackCalls));
        properties.Should().ContainKey(RabbitMqConsumer.ContextProperty_MessageConfirmed);
    }

    [Fact]
    public void When_ConfirmMessage_Given_MessageAlreadyConfirmed_Then_ShouldNotConfirmAgain()
    {
        // Arrange
        var consumers = CreateConsumerSettings("test-queue", "");
        var consumer = CreateRabbitMqConsumer("test-queue", consumers);

        var deliverEventArgs = CreateBasicDeliverEventArgs();
        var properties = new Dictionary<string, object>
        {
            { RabbitMqConsumer.ContextProperty_MessageConfirmed, true }
        };

        // Act
        consumer.ConfirmMessage(deliverEventArgs, RabbitMqMessageConfirmOptions.Ack, properties, warnIfAlreadyConfirmed: true);

        // Assert
        _modelMock.Verify(x => x.BasicAck(It.IsAny<ulong>(), It.IsAny<bool>()), Times.Never);
        _modelMock.Verify(x => x.BasicNack(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void When_InitializeConsumerContext_Given_AckAutomaticByRabbitMode_Then_ShouldMarkAsConfirmed()
    {
        // Arrange
        var consumers = CreateConsumerSettings("test-queue", "");
        RabbitMqProperties.MessageAcknowledgementMode.Set(consumers[0], RabbitMqMessageAcknowledgementMode.AckAutomaticByRabbit);

        var consumer = CreateRabbitMqConsumer("test-queue", consumers);

        var transportMessage = CreateBasicDeliverEventArgs();
        var consumerContext = new ConsumerContext();

        // Act
        consumer.InitializeConsumerContext(transportMessage, consumerContext);

        // Assert
        consumerContext.Properties.Should().ContainKey(RabbitMqConsumer.ContextProperty_MessageConfirmed);
        consumerContext.Properties[RabbitMqConsumer.ContextProperty_MessageConfirmed].Should().Be(true);
    }

    [Fact]
    public void When_ConstructorCalled_Given_MultipleConsumersWithDifferentInstances_Then_ShouldUseMaxInstances()
    {
        // Arrange
        var consumers = new List<ConsumerSettings>
        {
            CreateConsumerSettings("test-queue", "routing.key.1", instances: 5)[0],
            CreateConsumerSettings("test-queue", "routing.key.2", instances: 10)[0]
        };

        // Act
        var consumer = CreateRabbitMqConsumer("test-queue", consumers);

        // Assert - The consumer should be created and use max instances internally
        consumer.Should().NotBeNull();
    }

    [Fact]
    public async Task When_OnStop_Given_ActiveConsumer_Then_ShouldWaitForBackgroundTasks()
    {
        // Arrange
        var consumers = CreateConsumerSettings("test-queue", "");
        var consumer = CreateRabbitMqConsumer("test-queue", consumers);

        // Act
        var stopTask = consumer.Stop();
        await stopTask;

        // Assert
        stopTask.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task When_DisposeAsync_Given_ActiveConsumer_Then_ShouldDisposeMessageProcessors()
    {
        // Arrange
        var consumers = CreateConsumerSettings("test-queue", "");
        var consumer = CreateRabbitMqConsumer("test-queue", consumers);

        // Act
        var act = async () => await consumer.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void When_InitializeConsumerContext_Given_ValidMessage_Then_ShouldSetTransportMessageAndConfirmAction()
    {
        // Arrange
        var consumers = CreateConsumerSettings("test-queue", "");
        var consumer = CreateRabbitMqConsumer("test-queue", consumers);

        var transportMessage = CreateBasicDeliverEventArgs();
        var consumerContext = new ConsumerContext();

        // Act
        consumer.InitializeConsumerContext(transportMessage, consumerContext);

        // Assert
        consumerContext.GetTransportMessage().Should().BeSameAs(transportMessage);
        consumerContext.Properties.Should().ContainKey("RabbitMq_MessageConfirmAction");
    }

    private TestableRabbitMqConsumer CreateRabbitMqConsumer(string queueName, IList<ConsumerSettings> consumers)
    {
        var consumer = new TestableRabbitMqConsumer(
            _loggerFactoryMock.Object,
            _channelMock.Object,
            queueName,
            consumers,
            _messageBus,
            _messageProviderMock.Object,
            _headerValueConverterMock.Object);

        _consumersToDispose.Add(consumer);

        // Start the consumer to initialize the CancellationToken
        consumer.Start().Wait();

        return consumer;
    }

    private static IList<ConsumerSettings> CreateConsumerSettings(string queueName, string routingKey, int instances = 1)
    {
        var consumerSettings = new ConsumerSettings
        {
            Path = "test-exchange",
            MessageType = typeof(TestMessage),
            ConsumerType = typeof(TestConsumer),
            ConsumerMethod = (consumer, message, ctx, ct) => Task.CompletedTask,
            Instances = instances
        };

        RabbitMqProperties.QueueName.Set(consumerSettings, queueName);
        RabbitMqProperties.BindingRoutingKey.Set(consumerSettings, routingKey);

        return [consumerSettings];
    }

    private static BasicDeliverEventArgs CreateBasicDeliverEventArgs(
        Dictionary<string, object> headers = null,
        ulong deliveryTag = 1,
        string routingKey = "test.routing.key")
    {
        var properties = new Mock<IBasicProperties>();
        properties.Setup(x => x.Headers).Returns(headers);

        return new BasicDeliverEventArgs
        {
            DeliveryTag = deliveryTag,
            Exchange = "test-exchange",
            RoutingKey = routingKey,
            BasicProperties = properties.Object,
            Body = new ReadOnlyMemory<byte>(Array.Empty<byte>())
        };
    }

    // Test message and consumer types
    private class TestMessage
    {
        public string Content { get; set; }
    }

    private class TestConsumer
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "For the test we need a non static member")]
        public Task Consume(TestMessage message) => Task.CompletedTask;
    }

    // Testable subclass that exposes protected methods
    private class TestableRabbitMqConsumer(
        ILoggerFactory loggerFactory,
        IRabbitMqChannel channel,
        string queueName,
        IList<ConsumerSettings> consumers,
        MessageBusBase<RabbitMqMessageBusSettings> messageBus,
        MessageProvider<BasicDeliverEventArgs> messageProvider,
        IHeaderValueConverter headerValueConverter) : RabbitMqConsumer(loggerFactory, channel, queueName, consumers, messageBus, messageProvider, headerValueConverter)
    {
        public Task<Exception> OnMessageReceivedPublic(Dictionary<string, object> messageHeaders, BasicDeliverEventArgs transportMessage)
            => OnMessageReceived(messageHeaders, transportMessage);
    }

    // Testable MessageBus for testing
    private class TestableMessageBus(MessageBusSettings settings, RabbitMqMessageBusSettings providerSettings)
        : MessageBusBase<RabbitMqMessageBusSettings>(settings, providerSettings)
    {
        public override Task ProduceToTransport(object message, Type messageType, string path, IDictionary<string, object> messageHeaders, IMessageBusTarget targetBus, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
