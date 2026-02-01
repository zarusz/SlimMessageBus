namespace SlimMessageBus.Host.RabbitMQ.Test.Config;

public class RabbitMqProducerBuilderExtensionsTests
{
    private readonly MessageBusSettings _busSettings = new();
    private readonly RabbitMqMessageBusSettings _providerSettings = new();

    [Fact]
    internal void When_EnablePublisherConfirms_Then_ValueStoredProperly()
    {
        // arrange
        var builder = new ProducerBuilder<object>(new ProducerSettings());

        // act
        builder.EnablePublisherConfirms();

        // assert
        builder.Settings.GetOrDefault<bool?>(RabbitMqProperties.EnablePublisherConfirms, null).Should().BeTrue();
    }

    [Fact]
    internal void When_EnablePublisherConfirms_Given_False_Then_ValueStoredProperly()
    {
        // arrange
        var builder = new ProducerBuilder<object>(new ProducerSettings());

        // act
        builder.EnablePublisherConfirms(enabled: false);

        // assert
        builder.Settings.GetOrDefault<bool?>(RabbitMqProperties.EnablePublisherConfirms, null).Should().BeFalse();
    }

    [Fact]
    internal void When_IsPublisherConfirmsEnabled_Given_ProducerEnabled_And_BusDisabled_Then_ReturnsTrue()
    {
        // arrange
        var producerSettings = new ProducerSettings();
        RabbitMqProperties.EnablePublisherConfirms.Set(producerSettings, true);
        _providerSettings.EnablePublisherConfirms = false;

        // act
        var result = producerSettings.IsPublisherConfirmsEnabled(_providerSettings);

        // assert
        result.Should().BeTrue();
    }

    [Fact]
    internal void When_IsPublisherConfirmsEnabled_Given_ProducerDisabled_And_BusEnabled_Then_ReturnsFalse()
    {
        // arrange
        var producerSettings = new ProducerSettings();
        RabbitMqProperties.EnablePublisherConfirms.Set(producerSettings, false);
        _providerSettings.EnablePublisherConfirms = true;

        // act
        var result = producerSettings.IsPublisherConfirmsEnabled(_providerSettings);

        // assert
        result.Should().BeFalse();
    }

    [Fact]
    internal void When_IsPublisherConfirmsEnabled_Given_ProducerNotSet_And_BusEnabled_Then_ReturnsTrue()
    {
        // arrange
        var producerSettings = new ProducerSettings();
        _providerSettings.EnablePublisherConfirms = true;

        // act
        var result = producerSettings.IsPublisherConfirmsEnabled(_providerSettings);

        // assert
        result.Should().BeTrue();
    }

    [Fact]
    internal void When_IsPublisherConfirmsEnabled_Given_ProducerNotSet_And_BusDisabled_Then_ReturnsFalse()
    {
        // arrange
        var producerSettings = new ProducerSettings();
        _providerSettings.EnablePublisherConfirms = false;

        // act
        var result = producerSettings.IsPublisherConfirmsEnabled(_providerSettings);

        // assert
        result.Should().BeFalse();
    }
}
