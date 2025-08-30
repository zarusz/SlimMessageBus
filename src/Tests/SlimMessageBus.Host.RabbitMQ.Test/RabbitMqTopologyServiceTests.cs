namespace SlimMessageBus.Host.RabbitMQ.Test;

using System.Collections.Generic;

using AwesomeAssertions;

using global::RabbitMQ.Client;

using Microsoft.Extensions.Logging;

using Moq;

using SlimMessageBus.Host;
using SlimMessageBus.Host.RabbitMQ;

using Xunit;

public class RabbitMqTopologyServiceTests
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly Mock<IModel> _channelMock;
    private readonly RabbitMqMessageBusSettings _providerSettings;

    public RabbitMqTopologyServiceTests()
    {
        _loggerFactory = NullLoggerFactory.Instance;
        _channelMock = new Mock<IModel>();
        _providerSettings = new RabbitMqMessageBusSettings();
    }

    [Fact]
    public void ProvisionTopology_WithEmptyExchangeName_LogsSkippingExchange()
    {
        // Arrange
        var producer = new ProducerSettings { DefaultPath = "" };
        var settings = new MessageBusSettings();
        settings.Producers.Add(producer);
        settings.Consumers.Add(new ConsumerSettings
        {
            Path = "some-queue",
            MessageType = typeof(string),
            ConsumerType = typeof(object)
        });


        var service = new RabbitMqTopologyService(_loggerFactory, _channelMock.Object, settings, _providerSettings);

        // Act
        var result = service.ProvisionTopology();

        // Assert
        result.Should().BeTrue();
        _channelMock.Verify(x => x.ExchangeDeclare(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, object>>()), Times.Never);
    }

    [Fact]
    public void ProvisionTopology_WithNonEmptyExchangeName_DeclaresExchange()
    {
        // Arrange
        var producer = new ProducerSettings { DefaultPath = "my-exchange" };
        var settings = new MessageBusSettings();
        settings.Producers.Add(producer);
        settings.Consumers.Add(new ConsumerSettings
        {
            Path = "some-queue",
            MessageType = typeof(string),
            ConsumerType = typeof(object)
        });

        var service = new RabbitMqTopologyService(_loggerFactory, _channelMock.Object, settings, _providerSettings);

        // Act
        var result = service.ProvisionTopology();

        // Assert
        result.Should().BeTrue();
        _channelMock.Verify(x => x.ExchangeDeclare("my-exchange", It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, object>>()), Times.Once);
    }
}
