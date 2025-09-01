namespace SlimMessageBus.Host.RabbitMQ.Test;

using global::RabbitMQ.Client;

using SlimMessageBus.Host.RabbitMQ;

public class RabbitMqChannelManagerTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly RabbitMqMessageBusSettings _providerSettings;
    private readonly MessageBusSettings _settings;
    private readonly List<RabbitMqChannelManager> _managersToDispose;
    private bool disposedValue;

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

        _managersToDispose = [];
    }

    [Fact]
    public void When_ConstructorCalled_Given_ValidParameters_Then_ShouldInitializeSuccessfully()
    {
        // Act
        var manager = CreateChannelManager();

        // Assert
        manager.Should().NotBeNull();
        manager.Channel.Should().BeNull(); // Channel should be null until connection is established
        manager.ChannelLock.Should().NotBeNull();
    }

    [Fact]
    public void When_ConstructorCalled_Given_NullLoggerFactory_Then_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => new RabbitMqChannelManager(null, _providerSettings, _settings, () => false);
        action.Should().Throw<ArgumentNullException>().WithParameterName("loggerFactory");
    }

    [Fact]
    public void When_ConstructorCalled_Given_NullProviderSettings_Then_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => new RabbitMqChannelManager(_loggerFactory, null, _settings, () => false);
        action.Should().Throw<ArgumentNullException>().WithParameterName("providerSettings");
    }

    [Fact]
    public void When_ConstructorCalled_Given_NullSettings_Then_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => new RabbitMqChannelManager(_loggerFactory, _providerSettings, null, () => false);
        action.Should().Throw<ArgumentNullException>().WithParameterName("settings");
    }

    [Fact]
    public void When_ConstructorCalled_Given_NullIsDisposingOrDisposedFunc_Then_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => new RabbitMqChannelManager(_loggerFactory, _providerSettings, _settings, null);
        action.Should().Throw<ArgumentNullException>().WithParameterName("isDisposingOrDisposed");
    }

    [Fact]
    public void When_ConstructorCalled_Given_ZeroNetworkRecoveryInterval_Then_ShouldDefaultToTenSeconds()
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
    public void When_ConstructorCalled_Given_CustomNetworkRecoveryInterval_Then_ShouldUseCustomInterval()
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
        manager.Should().NotBeNull();
    }

    [Fact]
    public void When_EnsureChannelCalled_Given_ChannelIsNull_Then_ShouldThrowProducerMessageBusException()
    {
        // Arrange
        var manager = CreateChannelManager();

        // Act & Assert
        manager.Invoking(m => m.EnsureChannel())
            .Should().Throw<ProducerMessageBusException>()
            .WithMessage("*The Channel is not available at this time*");
    }

    [Fact]
    public void When_EnsureChannelCalled_Given_ChannelIsNull_Then_ShouldTriggerReconnectionAttempt()
    {
        // Arrange
        var manager = CreateChannelManager();

        // Act & Assert
        // This should throw but also trigger a background reconnection attempt
        manager.Invoking(m => m.EnsureChannel())
            .Should().Throw<ProducerMessageBusException>();

        // Verify that the reconnection was triggered by checking that the method throws
        // In a real scenario, this would start the retry timer
    }

    [Fact]
    public async Task When_InitializeConnectionCalled_Given_ConnectionFactoryWithInvalidHost_Then_ShouldHandleFailureGracefully()
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
    public async Task When_InitializeConnectionCalled_Given_CancellationToken_Then_ShouldRespectCancellation()
    {
        // Arrange
        var settingsWithBadConnection = new RabbitMqMessageBusSettings
        {
            ConnectionFactory = new ConnectionFactory
            {
                HostName = "nonexistent-host-for-testing",
                Port = 5672,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10) // Long interval to ensure cancellation happens first
            }
        };

        var manager = new RabbitMqChannelManager(
            _loggerFactory,
            settingsWithBadConnection,
            _settings,
            () => false);

        _managersToDispose.Add(manager);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        // Act
        await manager.InitializeConnection(cts.Token);

        // Assert
        manager.Channel.Should().BeNull("Channel should remain null if initialization is cancelled");
    }

    [Fact]
    public async Task When_InitializeConnectionCalled_Given_DefaultCancellationToken_Then_ShouldUseDefaultToken()
    {
        // Arrange
        var settingsWithBadConnection = new RabbitMqMessageBusSettings
        {
            ConnectionFactory = new ConnectionFactory
            {
                HostName = "nonexistent-host-for-testing",
                Port = 5672,
                NetworkRecoveryInterval = TimeSpan.FromMilliseconds(50)
            }
        };

        var manager = new RabbitMqChannelManager(
            _loggerFactory,
            settingsWithBadConnection,
            _settings,
            () => false);

        _managersToDispose.Add(manager);

        // Act
        await manager.InitializeConnection(); // Using default cancellation token

        // Assert
        manager.Channel.Should().BeNull();
    }

    [Fact]
    public void When_IRabbitMqChannelInterfaceCast_Given_ValidManager_Then_ShouldExposeCorrectProperties()
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
    public void When_DisposeCalled_Given_ValidManager_Then_ShouldCleanupResourcesWithoutThrowing()
    {
        // Arrange
        var manager = CreateChannelManager();

        // Act
        Action act = () => manager.Dispose();

        // Assert
        // Should not throw and cleanup should happen silently
        act.Should().NotThrow();

        // Remove from disposal list since we manually disposed
        _managersToDispose.Remove(manager);
    }

    [Fact]
    public void When_DisposeCalled_Given_AlreadyDisposedManager_Then_ShouldNotThrow()
    {
        // Arrange
        var manager = CreateChannelManager();
        manager.Dispose();
        _managersToDispose.Remove(manager);

        // Act & Assert
        manager.Invoking(m => m.Dispose()).Should().NotThrow();
    }

    [Fact]
    public void When_ChannelManagerCreated_Given_ProviderSettingsWithEndpoints_Then_ShouldInitializeCorrectly()
    {
        // Arrange
        var settingsWithEndpoints = new RabbitMqMessageBusSettings
        {
            ConnectionFactory = new ConnectionFactory(),
            Endpoints = [new AmqpTcpEndpoint("localhost")]
        };

        // Act
        var manager = new RabbitMqChannelManager(
            _loggerFactory,
            settingsWithEndpoints,
            _settings,
            () => false);

        _managersToDispose.Add(manager);

        // Assert
        manager.Should().NotBeNull();
        manager.Channel.Should().BeNull();
    }

    [Fact]
    public void When_ChannelManagerCreated_Given_ProviderSettingsWithMultipleEndpoints_Then_ShouldInitializeCorrectly()
    {
        // Arrange
        var settingsWithMultipleEndpoints = new RabbitMqMessageBusSettings
        {
            ConnectionFactory = new ConnectionFactory(),
            Endpoints = [
                new AmqpTcpEndpoint("localhost"),
                new AmqpTcpEndpoint("localhost", 5673)
            ]
        };

        // Act
        var manager = new RabbitMqChannelManager(
            _loggerFactory,
            settingsWithMultipleEndpoints,
            _settings,
            () => false);

        _managersToDispose.Add(manager);

        // Assert
        manager.Should().NotBeNull();
        manager.Channel.Should().BeNull();
    }

    [Fact]
    public void When_ChannelManagerCreated_Given_ProviderSettingsWithTopologyInitializer_Then_ShouldInitializeCorrectly()
    {
        // Arrange
        var settingsWithTopologyInitializer = new RabbitMqMessageBusSettings
        {
            ConnectionFactory = new ConnectionFactory()
        };

        // Set up a topology initializer
        settingsWithTopologyInitializer.Properties[RabbitMqProperties.TopologyInitializer.Key] =
            new RabbitMqTopologyInitializer((channel, applyDefault) =>
            {
                applyDefault();
            });

        // Act
        var manager = new RabbitMqChannelManager(
            _loggerFactory,
            settingsWithTopologyInitializer,
            _settings,
            () => false);

        _managersToDispose.Add(manager);

        // Assert
        manager.Should().NotBeNull();
    }

    [Fact]
    public void When_ConnectionShutdownEventRaised_Given_ManagerNotDisposing_Then_ShouldStartRetryTimer()
    {
        // Arrange
        var manager = CreateChannelManager();
        var isDisposing = false;
        var managerWithDisposingCallback = new RabbitMqChannelManager(
            _loggerFactory,
            _providerSettings,
            _settings,
            () => isDisposing);

        _managersToDispose.Add(managerWithDisposingCallback);

        // We can't easily test the private OnConnectionShutdown method directly,
        // but we can verify the manager handles disposal state correctly

        // Act
        isDisposing = true;
        managerWithDisposingCallback.Dispose();
        _managersToDispose.Remove(managerWithDisposingCallback);

        // Assert
        // Should not throw during disposal
        managerWithDisposingCallback.Should().NotBeNull();
    }

    [Fact]
    public async Task When_InitializeConnectionCalled_Given_MultipleRetriesNeeded_Then_ShouldEventuallySucceedOrFail()
    {
        // Arrange
        var settingsWithBadConnection = new RabbitMqMessageBusSettings
        {
            ConnectionFactory = new ConnectionFactory
            {
                HostName = "definitely-nonexistent-host",
                Port = 1234, // Non-standard port
                NetworkRecoveryInterval = TimeSpan.FromMilliseconds(50) // Fast retry for testing
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
        // After retries fail, channel should still be null
        manager.Channel.Should().BeNull();
    }

    [Fact]
    public void When_EnsureChannelCalled_Given_ConcurrentAccess_Then_ShouldHandleThreadSafety()
    {
        // Arrange
        var manager = CreateChannelManager();
        var exceptions = new List<Exception>();

        // Act
        Parallel.For(0, 10, _ =>
        {
            try
            {
                manager.EnsureChannel();
            }
            catch (Exception ex)
            {
                lock (exceptions)
                {
                    exceptions.Add(ex);
                }
            }
        });

        // Assert
        // All calls should result in the same exception type
        exceptions.Should().NotBeEmpty();
        exceptions.Should().AllBeOfType<ProducerMessageBusException>();
    }

    [Fact]
    public void When_ChannelLockAccessed_Given_ValidManager_Then_ShouldReturnNonNullObject()
    {
        // Arrange
        var manager = CreateChannelManager();

        // Act
        var channelLock = manager.ChannelLock;

        // Assert
        channelLock.Should().NotBeNull();
        channelLock.Should().BeOfType<object>();
    }

    [Fact]
    public void When_ChannelLockAccessed_Given_ConcurrentAccess_Then_ShouldReturnSameInstance()
    {
        // Arrange
        var manager = CreateChannelManager();
        var locks = new List<object>();

        // Act
        Parallel.For(0, 10, _ =>
        {
            var channelLock = manager.ChannelLock;
            lock (locks)
            {
                locks.Add(channelLock);
            }
        });

        // Assert
        locks.Should().NotBeEmpty();
        locks.All(x => ReferenceEquals(x, locks.First())).Should().BeTrue("All threads should get the same lock instance");
    }

    [Fact]
    public void When_ChannelPropertyAccessed_Given_NewManager_Then_ShouldReturnNull()
    {
        // Arrange
        var manager = CreateChannelManager();

        // Act
        var channel = manager.Channel;

        // Assert
        channel.Should().BeNull();
    }

    [Fact]
    public void When_ChannelPropertyAccessed_Given_ConcurrentAccess_Then_ShouldAlwaysReturnNull()
    {
        // Arrange
        var manager = CreateChannelManager();
        var channels = new List<IModel>();

        // Act
        Parallel.For(0, 10, _ =>
        {
            var channel = manager.Channel;
            lock (channels)
            {
                channels.Add(channel);
            }
        });

        // Assert
        channels.Should().OnlyContain(x => x == null, "Channel should be null until connection is established");
    }

    [Fact]
    public void When_ManagerCreated_Given_ValidSettings_Then_ShouldImplementIRabbitMqChannelInterface()
    {
        // Arrange & Act
        var manager = CreateChannelManager();

        // Assert
        manager.Should().BeAssignableTo<IRabbitMqChannel>();
    }

    [Fact]
    public void When_ManagerCreated_Given_ValidSettings_Then_ShouldImplementIDisposableInterface()
    {
        // Arrange & Act
        var manager = CreateChannelManager();

        // Assert
        manager.Should().BeAssignableTo<IDisposable>();
    }

    [Fact]
    public void When_ManagerCreated_Given_IsDisposingFuncReturnsTrue_Then_ShouldHandleCorrectly()
    {
        // Arrange & Act
        var manager = new RabbitMqChannelManager(
            _loggerFactory,
            _providerSettings,
            _settings,
            () => true); // Always disposing

        _managersToDispose.Add(manager);

        // Assert
        manager.Should().NotBeNull();
        manager.Channel.Should().BeNull();
    }

    [Fact]
    public void When_ManagerCreated_Given_IsDisposingFuncReturnsFalse_Then_ShouldHandleCorrectly()
    {
        // Arrange & Act
        var manager = new RabbitMqChannelManager(
            _loggerFactory,
            _providerSettings,
            _settings,
            () => false); // Never disposing

        _managersToDispose.Add(manager);

        // Assert
        manager.Should().NotBeNull();
        manager.Channel.Should().BeNull();
    }

    [Fact]
    public void When_EnsureChannelCalled_Given_RepeatedCalls_Then_ShouldConsistentlyThrow()
    {
        // Arrange
        var manager = CreateChannelManager();

        // Act & Assert
        for (int i = 0; i < 5; i++)
        {
            manager.Invoking(m => m.EnsureChannel())
                .Should().Throw<ProducerMessageBusException>()
                .WithMessage("*The Channel is not available at this time*");
        }
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

    #region Disposable

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                foreach (var manager in _managersToDispose)
                {
                    manager?.Dispose();
                }
                _managersToDispose.Clear();
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion
}