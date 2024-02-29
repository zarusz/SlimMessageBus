namespace SlimMessageBus.Host.RabbitMQ.Test.Config;

public class RabbitMqMessageBusSettingsExtensionsTests
{
    private readonly RabbitMqMessageBusSettings _settings = new();
    private readonly Fixture _fixture = new();

    [Fact]
    internal void When_UseAcknowledgementMode_Then_ValueStoredProperly()
    {
        // arrange
        var mode = _fixture.Create<RabbitMqMessageAcknowledgementMode>();

        // act
        _settings.AcknowledgementMode(mode);

        // assert
        var modeReturned = _settings.GetOrDefault<RabbitMqMessageAcknowledgementMode>(RabbitMqProperties.MessageAcknowledgementMode);
        modeReturned.Should().Be(mode);
    }

    [Fact]
    internal void When_UseRoutingKeyProvider_Then_ValueStoredProperly()
    {
        // arrange
        var routingKeyProviderMock = new Mock<RabbitMqMessageRoutingKeyProvider<object>>();

        // act
        _settings.UseRoutingKeyProvider(routingKeyProviderMock.Object);

        // assert
        var routingKeyProviderReturned = _settings.GetOrDefault<RabbitMqMessageRoutingKeyProvider<object>>(RabbitMqProperties.MessageRoutingKeyProvider);
        routingKeyProviderReturned.Should().BeSameAs(routingKeyProviderMock.Object);
    }
}
