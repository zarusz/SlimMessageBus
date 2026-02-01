namespace SlimMessageBus.Host.RabbitMQ.Test.Consumers;

using global::RabbitMQ.Client;
using global::RabbitMQ.Client.Events;

public class RabbitMqResponseConsumerTests
{
    private readonly Mock<IRabbitMqChannel> _channelMock = new();
    private readonly Mock<IChannel> _modelMock = new();
    private readonly Mock<IHeaderValueConverter> _headerValueConverterMock = new();

    public RabbitMqResponseConsumerTests()
    {
        _channelMock.Setup(x => x.Channel).Returns(_modelMock.Object);
        _modelMock.Setup(x => x.IsOpen).Returns(true);
        _modelMock
            .Setup(x => x.BasicConsumeAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<IAsyncBasicConsumer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-consumer-tag");

        _headerValueConverterMock.Setup(x => x.ConvertFrom(It.IsAny<object>())).Returns((object x) => x);
        _headerValueConverterMock.Setup(x => x.ConvertTo(It.IsAny<object>())).Returns((object x) => x);
    }

    [Fact]
    public async Task When_ResponseMessageMatchesPendingRequest_Then_AcksMessage()
    {
        var store = new InMemoryPendingRequestStore();
        var requestId = "req-1";
        store.Add(new PendingRequestState(
            id: requestId,
            request: new TestRequest(),
            requestType: typeof(TestRequest),
            responseType: typeof(TestResponse),
            created: DateTimeOffset.UtcNow,
            expires: DateTimeOffset.UtcNow.AddMinutes(1),
            cancellationToken: CancellationToken.None));

        var subject = CreateSubject(store, (_, _, _) => new TestResponse());
        await subject.Start();

        var transportMessage = CreateBasicDeliverEventArgs();
        var headers = new Dictionary<string, object> { [ReqRespMessageHeaders.RequestId] = requestId };

        var ex = await subject.InvokeCore(headers, transportMessage);

        ex.Should().BeNull();
        _modelMock.Verify(x => x.BasicAckAsync(transportMessage.DeliveryTag, false, It.IsAny<CancellationToken>()), Times.Once);
        _modelMock.Verify(x => x.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        store.GetCount().Should().Be(0);
    }

    [Fact]
    public async Task When_ResponseMessageHasNoRequestId_Then_NacksWithoutRequeue()
    {
        var subject = CreateSubject(new InMemoryPendingRequestStore(), (_, _, _) => new TestResponse());
        await subject.Start();

        var transportMessage = CreateBasicDeliverEventArgs();
        var headers = new Dictionary<string, object>();

        var ex = await subject.InvokeCore(headers, transportMessage);

        ex.Should().NotBeNull();
        _modelMock.Verify(x => x.BasicNackAsync(transportMessage.DeliveryTag, false, false, It.IsAny<CancellationToken>()), Times.Once);
        _modelMock.Verify(x => x.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task When_MessageProcessorReturnsRequeue_Then_NacksWithRequeue()
    {
        var subject = CreateSubject(new InMemoryPendingRequestStore(), (_, _, _) => new TestResponse());
        await subject.Start();

        var fakeProcessor = new Mock<IMessageProcessor<BasicDeliverEventArgs>>();
        fakeProcessor.SetupGet(x => x.ConsumerSettings).Returns([]);
        fakeProcessor
            .Setup(x => x.ProcessMessage(It.IsAny<BasicDeliverEventArgs>(), It.IsAny<IReadOnlyDictionary<string, object>>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<IServiceProvider>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessMessageResult(RabbitMqProcessResult.Requeue, null, null, null));

        typeof(RabbitMqResponseConsumer)
            .GetField("_messageProcessor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(subject, fakeProcessor.Object);

        var transportMessage = CreateBasicDeliverEventArgs();

        var ex = await subject.InvokeCore(new Dictionary<string, object>(), transportMessage);

        ex.Should().BeNull();
        _modelMock.Verify(x => x.BasicNackAsync(transportMessage.DeliveryTag, false, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    private TestRabbitMqResponseConsumer CreateSubject(IPendingRequestStore store, MessageProvider<BasicDeliverEventArgs> messageProvider)
    {
        var requestResponseSettings = new RequestResponseSettings { Path = "reply-queue" };
        return new TestRabbitMqResponseConsumer(
            NullLoggerFactory.Instance,
            Array.Empty<IAbstractConsumerInterceptor>(),
            _channelMock.Object,
            queueName: "reply-queue",
            requestResponseSettings,
            messageProvider,
            store,
            TimeProvider.System,
            _headerValueConverterMock.Object);
    }

    private static BasicDeliverEventArgs CreateBasicDeliverEventArgs(ulong deliveryTag = 1)
    {
        var properties = new Mock<IReadOnlyBasicProperties>();
        properties.Setup(x => x.Headers).Returns(new Dictionary<string, object>());

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

    private class TestRabbitMqResponseConsumer(
        ILoggerFactory loggerFactory,
        IEnumerable<IAbstractConsumerInterceptor> interceptors,
        IRabbitMqChannel channel,
        string queueName,
        RequestResponseSettings requestResponseSettings,
        MessageProvider<BasicDeliverEventArgs> messageProvider,
        IPendingRequestStore pendingRequestStore,
        TimeProvider timeProvider,
        IHeaderValueConverter headerValueConverter)
        : RabbitMqResponseConsumer(loggerFactory, interceptors, channel, queueName, requestResponseSettings, messageProvider, pendingRequestStore, timeProvider, headerValueConverter)
    {
        public Task<Exception> InvokeCore(Dictionary<string, object> headers, BasicDeliverEventArgs transportMessage)
            => OnMessageReceived(headers, transportMessage);
    }

    private record TestRequest;
    private record TestResponse;
}
