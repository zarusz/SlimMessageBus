namespace SlimMessageBus.Host.RabbitMQ.Test.Consumer;

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

        _transportMessage = new BasicDeliverEventArgs
        {
            Exchange = "exchange",
            DeliveryTag = 1
        };
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

    [Theory]
    [InlineData(ProcessResult.Abandon, RabbitMqMessageConfirmOptions.Nack)]
    [InlineData(ProcessResult.Fail, RabbitMqMessageConfirmOptions.Nack)]
    [InlineData(ProcessResult.Success, RabbitMqMessageConfirmOptions.Ack)]
    public async Task When_ProcessMessage_Then_AutoAcknowledge(ProcessResult processResult, RabbitMqMessageConfirmOptions expected)
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
    }

    private RabbitMqAutoAcknowledgeMessageProcessor CreateSubject()
    {
        return new RabbitMqAutoAcknowledgeMessageProcessor(_messageProcessorMock.Object, NullLogger<RabbitMqAutoAcknowledgeMessageProcessor>.Instance, RabbitMqMessageAcknowledgementMode.ConfirmAfterMessageProcessingWhenNoManualConfirmMade, _consumerMock.Object);
    }
}
