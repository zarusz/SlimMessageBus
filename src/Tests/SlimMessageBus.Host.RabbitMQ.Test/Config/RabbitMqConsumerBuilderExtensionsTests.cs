namespace SlimMessageBus.Host.RabbitMQ.Test.Config;

public class RabbitMqConsumerBuilderExtensionsTests
{
    private readonly MessageBusSettings _settings = new();
    private readonly ConsumerBuilder<object> _consumerBuilder;
    private readonly Fixture _fixture = new();

    public RabbitMqConsumerBuilderExtensionsTests()
    {
        _consumerBuilder = new ConsumerBuilder<object>(_settings);
    }

    [Fact]
    internal void When_UseAcknowledgementMode_Then_ValueStoredProperly()
    {
        // arrange
        var mode = _fixture.Create<RabbitMqMessageAcknowledgementMode>();

        // act
        _consumerBuilder.AcknowledgementMode(mode);

        // assert
        var modeReturned = _consumerBuilder.ConsumerSettings.GetOrDefault(RabbitMqProperties.MessageAcknowledgementMode);
        modeReturned.Should().Be(mode);
    }
}