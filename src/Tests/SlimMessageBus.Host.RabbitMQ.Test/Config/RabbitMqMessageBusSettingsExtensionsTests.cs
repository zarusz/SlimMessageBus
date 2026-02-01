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
        var modeReturned = _settings.GetOrDefault(RabbitMqProperties.MessageAcknowledgementMode);
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
        var routingKeyProviderReturned = _settings.GetOrDefault(RabbitMqProperties.MessageRoutingKeyProvider);
        routingKeyProviderReturned.Should().BeSameAs(routingKeyProviderMock.Object);
    }

    [Fact]
    internal void When_UsePublisherConfirms_Then_EnabledWithDefaultTimeout()
    {
        // act
        _settings.UsePublisherConfirms();

        // assert
        _settings.EnablePublisherConfirms.Should().BeTrue();
        _settings.PublisherConfirmsTimeout.Should().Be(TimeSpan.FromSeconds(10));
    }

    [Fact]
    internal void When_UsePublisherConfirms_Given_Timeout_Then_EnabledWithTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(30);

        // act
        _settings.UsePublisherConfirms(timeout);

        // assert
        _settings.EnablePublisherConfirms.Should().BeTrue();
        _settings.PublisherConfirmsTimeout.Should().Be(timeout);
    }

    [Fact]
    internal void When_UsePublisherConfirms_Given_NullSettings_Then_ShouldThrowArgumentNullException()
    {
        // arrange
        RabbitMqMessageBusSettings nullSettings = null;

        // act
        var act = () => nullSettings.UsePublisherConfirms();

        // assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("settings");
    }
}
