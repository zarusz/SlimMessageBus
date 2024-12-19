namespace SlimMessageBus.Host.RabbitMQ.Test.Consumer;

using global::RabbitMQ.Client.Events;

public class RabbitMqAutoAcknowledgeMessageProcessorTests
{
    private readonly Mock<IMessageProcessor<object>> _targetMock;
    private readonly Mock<IDisposable> _targetDisposableMock;
    private readonly Mock<IRabbitMqConsumer> _consumerMock;
    private readonly BasicDeliverEventArgs _transportMessage;
    private readonly RabbitMqAutoAcknowledgeMessageProcessor _subject;

    public RabbitMqAutoAcknowledgeMessageProcessorTests()
    {
        _targetMock = new Mock<IMessageProcessor<object>>();
        _targetDisposableMock = _targetMock.As<IDisposable>();
        _consumerMock = new Mock<IRabbitMqConsumer>();

        _transportMessage = new BasicDeliverEventArgs
        {
            Exchange = "exchange",
            DeliveryTag = 1
        };

        _subject = new RabbitMqAutoAcknowledgeMessageProcessor(_targetMock.Object, NullLogger<RabbitMqAutoAcknowledgeMessageProcessor>.Instance, RabbitMqMessageAcknowledgementMode.ConfirmAfterMessageProcessingWhenNoManualConfirmMade, _consumerMock.Object);
    }

    [Fact]
    public void When_Dispose_Then_CallsDisposeOnTarget()
    {
        // arrange

        // act
        _subject.Dispose();

        // assert
        _targetDisposableMock.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task When_ProcessMessage_Then_AutoAcknowledge()
    {
        // arrange

        // act
        var result = await _subject.ProcessMessage(
            _transportMessage,
            new Dictionary<string, object>(),
            null,
            null,
            CancellationToken.None);

        // assert
        _consumerMock.Verify(x => x.ConfirmMessage(
            _transportMessage,
            RabbitMqMessageConfirmOptions.Ack,
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<bool>()), Times.Once);
    }
}
