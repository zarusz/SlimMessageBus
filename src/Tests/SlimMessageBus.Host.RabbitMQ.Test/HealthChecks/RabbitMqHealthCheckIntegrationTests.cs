namespace SlimMessageBus.Host.RabbitMQ.Test.HealthChecks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using SlimMessageBus.Host.RabbitMQ.HealthChecks;

public class RabbitMqHealthCheckIntegrationTests
{
    [Fact]
    public void AddRabbitMqHealthCheck_Should_Register_Services_Correctly()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Add required dependencies
        services.AddLogging();
        services.AddSingleton<IRabbitMqChannel>(Mock.Of<IRabbitMqChannel>());
        
        // Add health checks
        services.AddHealthChecks().AddRabbitMq();
        services.AddRabbitMqHealthCheck();

        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        var healthCheck = serviceProvider.GetService<RabbitMqHealthCheck>();
        healthCheck.Should().NotBeNull();

        var healthCheckService = serviceProvider.GetService<HealthCheckService>();
        healthCheckService.Should().NotBeNull();
    }

    [Fact]
    public void AddRabbitMqHealthCheck_With_Factory_Should_Register_Services_Correctly()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Add required dependencies
        services.AddLogging();
        
        var channelMock = Mock.Of<IRabbitMqChannel>();
        
        // Add health checks with factory
        services.AddHealthChecks().AddRabbitMq(
            serviceProvider => channelMock,
            name: "rabbitmq-custom");

        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        var healthCheckService = serviceProvider.GetService<HealthCheckService>();
        healthCheckService.Should().NotBeNull();
    }

    [Fact]
    public async Task HealthCheck_Integration_With_Real_IRabbitMqChannel_Interface()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Create a mock IRabbitMqChannel that simulates a healthy channel
        var channelMock = new Mock<global::RabbitMQ.Client.IModel>();
        channelMock.SetupGet(x => x.IsOpen).Returns(true);
        channelMock.SetupGet(x => x.ChannelNumber).Returns(1);

        var rabbitMqChannelMock = new Mock<IRabbitMqChannel>();
        rabbitMqChannelMock.Setup(x => x.Channel).Returns(channelMock.Object);
        rabbitMqChannelMock.Setup(x => x.ChannelLock).Returns(new object());

        services.AddSingleton(rabbitMqChannelMock.Object);
        services.AddHealthChecks().AddRabbitMq();
        services.AddRabbitMqHealthCheck();

        var serviceProvider = services.BuildServiceProvider();
        var healthCheckService = serviceProvider.GetRequiredService<HealthCheckService>();

        // Act
        var result = await healthCheckService.CheckHealthAsync();

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Entries.Should().ContainKey("rabbitmq");
        result.Entries["rabbitmq"].Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task HealthCheck_Integration_With_Unhealthy_Channel()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Create a mock IRabbitMqChannel that simulates an unhealthy channel
        var rabbitMqChannelMock = new Mock<IRabbitMqChannel>();
        rabbitMqChannelMock.Setup(x => x.Channel).Returns((global::RabbitMQ.Client.IModel)null);

        services.AddSingleton(rabbitMqChannelMock.Object);
        services.AddHealthChecks().AddRabbitMq();
        services.AddRabbitMqHealthCheck();

        var serviceProvider = services.BuildServiceProvider();
        var healthCheckService = serviceProvider.GetRequiredService<HealthCheckService>();

        // Act
        var result = await healthCheckService.CheckHealthAsync();

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Entries.Should().ContainKey("rabbitmq");
        result.Entries["rabbitmq"].Status.Should().Be(HealthStatus.Unhealthy);
    }
}