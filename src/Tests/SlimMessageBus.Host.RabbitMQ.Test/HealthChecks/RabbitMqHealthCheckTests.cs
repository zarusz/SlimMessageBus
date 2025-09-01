namespace SlimMessageBus.Host.RabbitMQ.Test.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using global::RabbitMQ.Client;
using SlimMessageBus.Host.RabbitMQ.HealthChecks;

public class RabbitMqHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_Should_Return_Unhealthy_When_Channel_Is_Null()
    {
        // Arrange
        var channelMock = new Mock<IRabbitMqChannel>();
        channelMock.Setup(x => x.Channel).Returns((IModel)null);
        
        var loggerMock = new Mock<ILogger<RabbitMqHealthCheck>>();
        var healthCheck = new RabbitMqHealthCheck(channelMock.Object, loggerMock.Object);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("channel is not available");
    }

    [Fact]
    public async Task CheckHealthAsync_Should_Return_Unhealthy_When_Channel_Is_Closed()
    {
        // Arrange
        var channelMock = new Mock<IModel>();
        channelMock.SetupGet(x => x.IsOpen).Returns(false);
        channelMock.SetupGet(x => x.ChannelNumber).Returns(123);
        channelMock.SetupGet(x => x.CloseReason).Returns(new ShutdownEventArgs(ShutdownInitiator.Library, 123, "Test close"));

        var rabbitMqChannelMock = new Mock<IRabbitMqChannel>();
        rabbitMqChannelMock.Setup(x => x.Channel).Returns(channelMock.Object);
        
        var loggerMock = new Mock<ILogger<RabbitMqHealthCheck>>();
        var healthCheck = new RabbitMqHealthCheck(rabbitMqChannelMock.Object, loggerMock.Object);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("channel is closed");
        result.Data.Should().ContainKey("ChannelNumber");
        result.Data["ChannelNumber"].Should().Be(123);
    }

    [Fact]
    public async Task CheckHealthAsync_Should_Return_Healthy_When_Channel_Is_Open()
    {
        // Arrange
        var channelMock = new Mock<IModel>();
        channelMock.SetupGet(x => x.IsOpen).Returns(true);
        channelMock.SetupGet(x => x.ChannelNumber).Returns(456);

        var rabbitMqChannelMock = new Mock<IRabbitMqChannel>();
        rabbitMqChannelMock.Setup(x => x.Channel).Returns(channelMock.Object);
        
        var loggerMock = new Mock<ILogger<RabbitMqHealthCheck>>();
        var healthCheck = new RabbitMqHealthCheck(rabbitMqChannelMock.Object, loggerMock.Object);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("healthy");
        result.Data.Should().ContainKey("ChannelNumber");
        result.Data["ChannelNumber"].Should().Be(456);
        result.Data.Should().ContainKey("ChannelIsOpen");
        result.Data["ChannelIsOpen"].Should().Be(true);
    }

    [Fact]
    public async Task CheckHealthAsync_Should_Return_Unhealthy_When_Exception_Thrown()
    {
        // Arrange
        var rabbitMqChannelMock = new Mock<IRabbitMqChannel>();
        rabbitMqChannelMock.Setup(x => x.Channel).Throws(new InvalidOperationException("Test exception"));
        
        var loggerMock = new Mock<ILogger<RabbitMqHealthCheck>>();
        var healthCheck = new RabbitMqHealthCheck(rabbitMqChannelMock.Object, loggerMock.Object);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("exception");
        result.Exception.Should().BeOfType<InvalidOperationException>();
        result.Data.Should().ContainKey("Exception");
    }
}