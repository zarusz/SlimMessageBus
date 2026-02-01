namespace SlimMessageBus.Host.RabbitMQ.Test.Consumer;

using global::RabbitMQ.Client;
using global::RabbitMQ.Client.Events;

public class RabbitMqAutoAcknowledgeMessageProcessorTests
{
    private readonly Mock<IMessageProcessor<object>> _messageProcessorMock;
    private readonly Mock<IDisposable> _messageProcessorDisposableMock;
    private readonly Mock<IRabbitMqConsumer> _consumerMock;
    private readonly BasicDeliverEventArgs _transportMessage;

    public RabbitMqAutoAcknowledgeMessageProcessorTests()
    {
        _messageProcessorMock = new Mock<IMessageProcessor<object>>();
        _messageProcessorDisposableMock = _messageProcessorMock.As<IDisposable>();
        _consumerMock = new Mock<IRabbitMqConsumer>();

        var properties = new Mock<IReadOnlyBasicProperties>();
        _transportMessage = new BasicDeliverEventArgs(
            consumerTag: "test-consumer-tag",
            deliveryTag: 1,
            redelivered: false,
            exchange: "exchange",
            routingKey: "test.routing.key",
            properties: properties.Object,
            body: new ReadOnlyMemory<byte>(Array.Empty<byte>()),
            cancellationToken: default);
    }

    [Fact]
    public void When_Dispose_Then_CallsDisposeOnTarget()
    {
        // arrange
        var subject = CreateSubject();

        // act
        subject.Dispose();

        // assert
        _messageProcessorDisposableMock.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public Task When_Requeue_ThenAutoNackWithRequeue()
    {
        return When_ProcessMessage_Then_AutoAcknowledge(RabbitMqProcessResult.Requeue, RabbitMqMessageConfirmOptions.Nack | RabbitMqMessageConfirmOptions.Requeue);
    }

    [Fact]
    public Task When_Failure_ThenAutoNack()
    {
        return When_ProcessMessage_Then_AutoAcknowledge(RabbitMqProcessResult.Failure, RabbitMqMessageConfirmOptions.Nack);
    }

    [Fact]
    public Task When_Success_AutoAcknowledge()
    {
        return When_ProcessMessage_Then_AutoAcknowledge(RabbitMqProcessResult.Success, RabbitMqMessageConfirmOptions.Ack);
    }

    private async Task When_ProcessMessage_Then_AutoAcknowledge(ProcessResult processResult, RabbitMqMessageConfirmOptions expected)
    {
        // arrange
        _messageProcessorMock
            .Setup(x => x.ProcessMessage(It.IsAny<object>(), It.IsAny<IReadOnlyDictionary<string, object>>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<IServiceProvider>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ProcessMessageResult(processResult, null, null, null)));

        var subject = CreateSubject();

        // act
        var result = await subject.ProcessMessage(
            _transportMessage,
            new Dictionary<string, object>(),
            null,
            null,
            CancellationToken.None);

        // assert
        _consumerMock.Verify(x => x.ConfirmMessage(
            _transportMessage,
            expected,
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<bool>()), Times.Once);

        result.Result.Should().Be(processResult);
    }

    private RabbitMqAutoAcknowledgeMessageProcessor CreateSubject()
    {
        return new RabbitMqAutoAcknowledgeMessageProcessor(_messageProcessorMock.Object, NullLogger<RabbitMqAutoAcknowledgeMessageProcessor>.Instance, RabbitMqMessageAcknowledgementMode.ConfirmAfterMessageProcessingWhenNoManualConfirmMade, _consumerMock.Object);
    }
}
