namespace SlimMessageBus.Host.RabbitMQ.Test.Config;

using global::RabbitMQ.Client;
using global::RabbitMQ.Client.Events;

public class RabbitMqConsumerContextExtensionsTests
{
    private readonly IConsumerContext _consumerContext = new ConsumerContext();

    [Fact]
    internal void When_GetTransportMessage_Then_ReturnsMessage_Given_SetTransportMessageWasCalled()
    {
        // arrange
        // Create a real BasicDeliverEventArgs instead of mocking (it doesn't have a parameterless constructor)
        var propertiesMock = new Mock<IReadOnlyBasicProperties>();
        var transportMessage = new BasicDeliverEventArgs(
            consumerTag: "test-consumer",
            deliveryTag: 1,
            redelivered: false,
            exchange: "test-exchange",
            routingKey: "test.routing.key",
            properties: propertiesMock.Object,
            body: new ReadOnlyMemory<byte>(Array.Empty<byte>()),
            cancellationToken: default);

        _consumerContext.SetTransportMessage(transportMessage);

        // act
        var transportMessageReturend = _consumerContext.GetTransportMessage();

        // assert
        transportMessageReturend.Should().BeSameAs(transportMessage);
    }

    [Theory]
    [InlineData(false, RabbitMqMessageConfirmOptions.Ack)]
    [InlineData(true, RabbitMqMessageConfirmOptions.Ack)]
    [InlineData(true, RabbitMqMessageConfirmOptions.Nack)]
    [InlineData(true, RabbitMqMessageConfirmOptions.Nack | RabbitMqMessageConfirmOptions.Requeue)]
    internal void When_ConfirmAction_Then_CallsConfrirmMessageAction_Given_SetConfirmActionWasCalled(bool setConfirmActionMade, RabbitMqMessageConfirmOptions confirmOption)
    {
        // arrange
        var confirmActionMock = new Mock<RabbitMqMessageConfirmAction>();

        if (setConfirmActionMade)
        {
            _consumerContext.SetConfirmAction(confirmActionMock.Object);
        }

        // act
        var act = () =>
        {
            if ((confirmOption & RabbitMqMessageConfirmOptions.Ack) != 0)
            {
                _consumerContext.Ack();
            }
            if ((confirmOption & RabbitMqMessageConfirmOptions.Nack) != 0)
            {
                _consumerContext.Nack();
            }
            if ((confirmOption & RabbitMqMessageConfirmOptions.Nack) != 0 && (confirmOption & RabbitMqMessageConfirmOptions.Requeue) != 0)
            {
                _consumerContext.NackWithRequeue();
            }
        };

        // assert
        if (setConfirmActionMade)
        {
            act.Should().NotThrow();
            confirmActionMock.Verify(x => x(confirmOption), Times.Once);
        }
        else
        {
            act.Should().Throw<ConsumerMessageBusException>();
        }
    }
}
