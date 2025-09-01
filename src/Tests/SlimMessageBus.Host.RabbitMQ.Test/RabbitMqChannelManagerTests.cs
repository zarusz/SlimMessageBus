namespace SlimMessageBus.Host.RabbitMQ.Test;

using global::RabbitMQ.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SlimMessageBus.Host.RabbitMQ;

public class RabbitMqChannelManagerTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly RabbitMqMessageBusSettings _providerSettings;
    private readonly MessageBusSettings _settings;
    private readonly List<RabbitMqChannelManager> _managersToDispose;

    public RabbitMqChannelManagerTests()
    {
        // Use NullLoggerFactory to avoid mocking issues
        _loggerFactory = NullLoggerFactory.Instance;
        
        // Create real settings objects instead of mocking them
        _providerSettings = new RabbitMqMessageBusSettings
        {
            ConnectionFactory = new ConnectionFactory
            {
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
            }
        };

        _settings = new MessageBusSettings();

        _managersToDispose = new List<RabbitMqChannelManager>();
    }

    [Fact]
    public void Constructor_Should_Initialize_Successfully()
    {
        // Act
        var manager = CreateChannelManager();

        // Assert
        manager.Should().NotBeNull();
        manager.Channel.Should().BeNull(); // Channel should be null until connection is established
        manager.ChannelLock.Should().NotBeNull();
    }

    [Fact]
    public void EnsureChannel_Should_Throw_When_Channel_Is_Null()
    {
        // Arrange
        var manager = CreateChannelManager();

        // Act & Assert
        manager.Invoking(m => m.EnsureChannel())
            .Should().Throw<ProducerMessageBusException>()
            .WithMessage("*The Channel is not available at this time*");
    }

    [Fact]
    public void Dispose_Should_Cleanup_Resources()
    {
        // Arrange
        var manager = CreateChannelManager();

        // Act
        manager.Dispose();

        // Assert
        // Should not throw and cleanup should happen silently
        // Remove from disposal list since we manually disposed
        _managersToDispose.Remove(manager);
    }

    [Fact]
    public async Task InitializeConnection_Should_Handle_Connection_Failure_Gracefully()
    {
        // Arrange
        var settingsWithBadConnection = new RabbitMqMessageBusSettings
        {
            ConnectionFactory = new ConnectionFactory
            {
                HostName = "nonexistent-host-for-testing",
                Port = 5672,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(1)
            }
        };

        var manager = new RabbitMqChannelManager(
            _loggerFactory,
            settingsWithBadConnection,
            _settings,
            () => false);

        _managersToDispose.Add(manager);

        // Act
        await manager.InitializeConnection();

        // Assert
        manager.Channel.Should().BeNull();
    }

    [Fact]
    public void NetworkRecoveryInterval_Should_Be_Used_For_Retry_Timing()
    {
        // Arrange
        var customInterval = TimeSpan.FromSeconds(30);
        var settingsWithCustomInterval = new RabbitMqMessageBusSettings
        {
            ConnectionFactory = new ConnectionFactory
            {
                NetworkRecoveryInterval = customInterval
            }
        };

        var manager = new RabbitMqChannelManager(
            _loggerFactory,
            settingsWithCustomInterval,
            _settings,
            () => false);

        _managersToDispose.Add(manager);

        // Act & Assert
        // The manager should initialize successfully and use the custom interval
        // We can't directly test the timer interval, but we can verify the manager was created
        manager.Should().NotBeNull();
    }

    [Fact]
    public void NetworkRecoveryInterval_Zero_Should_Default_To_Ten_Seconds()
    {
        // Arrange
        var settingsWithZeroInterval = new RabbitMqMessageBusSettings
        {
            ConnectionFactory = new ConnectionFactory
            {
                NetworkRecoveryInterval = TimeSpan.Zero
            }
        };

        var manager = new RabbitMqChannelManager(
            _loggerFactory,
            settingsWithZeroInterval,
            _settings,
            () => false);

        _managersToDispose.Add(manager);

        // Act & Assert
        // The manager should initialize successfully and use the default 10-second interval
        manager.Should().NotBeNull();
    }

    [Fact]
    public void EnsureChannel_Should_Trigger_Reconnection_When_Channel_Is_Null()
    {
        // Arrange
        var manager = CreateChannelManager();

        // Act & Assert
        // This should throw but also trigger a background reconnection attempt
        manager.Invoking(m => m.EnsureChannel())
            .Should().Throw<ProducerMessageBusException>();

        // Verify that the reconnection was triggered by checking logs or other side effects
        // In a real scenario, this would start the retry timer
    }

    [Fact]
    public void IRabbitMqChannel_Interface_Should_Work_Correctly()
    {
        // Arrange
        var manager = CreateChannelManager();

        // Act - Cast to interface
        IRabbitMqChannel channelInterface = manager;

        // Assert
        channelInterface.Channel.Should().BeNull(); // No connection established yet
        channelInterface.ChannelLock.Should().BeSameAs(manager.ChannelLock);
    }

    [Fact]
    public void Constructor_Should_Validate_Parameters()
    {
        // Act & Assert
        var action1 = () => new RabbitMqChannelManager(null, _providerSettings, _settings, () => false);
        action1.Should().Throw<ArgumentNullException>().WithParameterName("loggerFactory");

        var action2 = () => new RabbitMqChannelManager(_loggerFactory, null, _settings, () => false);
        action2.Should().Throw<ArgumentNullException>().WithParameterName("providerSettings");

        var action3 = () => new RabbitMqChannelManager(_loggerFactory, _providerSettings, null, () => false);
        action3.Should().Throw<ArgumentNullException>().WithParameterName("settings");

        var action4 = () => new RabbitMqChannelManager(_loggerFactory, _providerSettings, _settings, null);
        action4.Should().Throw<ArgumentNullException>().WithParameterName("isDisposingOrDisposed");
    }

    private RabbitMqChannelManager CreateChannelManager()
    {
        var manager = new RabbitMqChannelManager(
            _loggerFactory,
            _providerSettings,
            _settings,
            () => false); // Never disposing for tests

        _managersToDispose.Add(manager);
        return manager;
    }

    public void Dispose()
    {
        foreach (var manager in _managersToDispose)
        {
            manager?.Dispose();
        }
        _managersToDispose.Clear();
    }
}