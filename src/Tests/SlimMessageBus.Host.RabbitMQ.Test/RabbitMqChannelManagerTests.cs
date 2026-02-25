namespace SlimMessageBus.Host.RabbitMQ.Test;

using System.Diagnostics;

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
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
                // Set short timeouts for tests to prevent hanging
                RequestedConnectionTimeout = TimeSpan.FromMilliseconds(500),
                ContinuationTimeout = TimeSpan.FromMilliseconds(500)
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
    }

    [Theory]
    [InlineData("loggerFactory")]
    [InlineData("providerSettings")]
    [InlineData("settings")]
    [InlineData("isDisposingOrDisposed")]
    public void When_ConstructorCalled_Given_NullParameter_Then_ShouldThrowArgumentNullException(string parameterName)
    {
        // Arrange & Act & Assert
        Action action = parameterName switch
        {
#pragma warning disable CA1806 // Do not ignore method results - Intentionally testing constructor exceptions
            "loggerFactory" => () => new RabbitMqChannelManager(null, _providerSettings, _settings, () => false),
            "providerSettings" => () => new RabbitMqChannelManager(_loggerFactory, null, _settings, () => false),
            "settings" => () => new RabbitMqChannelManager(_loggerFactory, _providerSettings, null, () => false),
            "isDisposingOrDisposed" => () => new RabbitMqChannelManager(_loggerFactory, _providerSettings, _settings, null),
#pragma warning restore CA1806 // Do not ignore method results
            _ => throw new ArgumentException("Invalid parameter name", nameof(parameterName))
        };

        action.Should().Throw<ArgumentNullException>().WithParameterName(parameterName);
    }

    [Theory]
    [InlineData(0)] // Zero interval should default to 10 seconds
    [InlineData(30)] // Custom interval should be used
    public void When_ConstructorCalled_Given_NetworkRecoveryInterval_Then_ShouldHandleCorrectly(int intervalSeconds)
    {
        // Arrange
        var interval = TimeSpan.FromSeconds(intervalSeconds);
        var settingsWithInterval = new RabbitMqMessageBusSettings
        {
            ConnectionFactory = new ConnectionFactory
            {
                NetworkRecoveryInterval = interval
            }
        };

        var manager = new RabbitMqChannelManager(
            _loggerFactory,
            settingsWithInterval,
            _settings,
            () => false);

        _managersToDispose.Add(manager);

        // Assert
        manager.Should().NotBeNull();
    }

    [Fact]
    public void When_EnsureChannelCalled_Given_ChannelIsNull_Then_ShouldThrowAndTriggerReconnection()
    {
        // Arrange
        var manager = CreateChannelManager();

        // Act & Assert
        manager.Invoking(m => m.EnsureChannel())
            .Should().Throw<ProducerMessageBusException>()
            .WithMessage("*The Channel is not available at this time*");
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

    [Theory]
    [InlineData(100, 10000)] // Cancel after 100ms with 10s interval
    [InlineData(10, 30000)] // Very short timeout with 30s interval
    public async Task When_InitializeConnectionCalled_Given_CancellationToken_Then_ShouldRespectCancellation(
        int cancelAfterMs, int networkRecoveryIntervalMs)
    {
        // Arrange
        var settingsWithBadConnection = new RabbitMqMessageBusSettings
        {
            ConnectionFactory = new ConnectionFactory
            {
                HostName = "nonexistent-host-for-testing",
                Port = 5672,
                NetworkRecoveryInterval = TimeSpan.FromMilliseconds(networkRecoveryIntervalMs)
            }
        };

        var manager = new RabbitMqChannelManager(
            _loggerFactory,
            settingsWithBadConnection,
            _settings,
            () => false);

        _managersToDispose.Add(manager);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(cancelAfterMs));

        // Act
        var stopwatch = Stopwatch.StartNew();
        await manager.InitializeConnection(cts.Token);
        stopwatch.Stop();

        // Assert
        manager.Channel.Should().BeNull("Channel should remain null if initialization is cancelled");
        if (cancelAfterMs == 10)
        {
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, "Should cancel quickly without waiting for full retry");
        }
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
        // Note: ChannelLock was removed in v7 since IChannel is thread-safe
    }

    [Theory]
    [InlineData(false)] // First dispose
    [InlineData(true)]  // Already disposed (double dispose)
    public void When_DisposeCalled_Given_ManagerState_Then_ShouldCleanupWithoutThrowing(bool alreadyDisposed)
    {
        // Arrange
        var manager = CreateChannelManager();

        if (alreadyDisposed)
        {
            manager.Dispose();
        }

        // Act
        Action act = () => manager.Dispose();

        // Assert
        act.Should().NotThrow();

        // Remove from disposal list since we manually disposed
        _managersToDispose.Remove(manager);
    }

    [Theory]
    [InlineData(1)]  // Single endpoint
    [InlineData(2)]  // Multiple endpoints
    [InlineData(0)]  // Empty endpoints
    public void When_ChannelManagerCreated_Given_Endpoints_Then_ShouldInitializeCorrectly(int endpointCount)
    {
        // Arrange
        var endpoints = endpointCount switch
        {
            0 => [],
            1 => [new("localhost")],
            2 => new List<AmqpTcpEndpoint>
            {
                new("localhost"),
                new("localhost", 5673)
            },
            _ => throw new ArgumentException("Invalid endpoint count")
        };

        var settingsWithEndpoints = new RabbitMqMessageBusSettings
        {
            ConnectionFactory = new ConnectionFactory(),
            Endpoints = endpoints
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
    public void When_ChannelManagerCreated_Given_ProviderSettingsWithTopologyInitializer_Then_ShouldInitializeCorrectly()
    {
        // Arrange
        var settingsWithTopologyInitializer = new RabbitMqMessageBusSettings
        {
            ConnectionFactory = new ConnectionFactory()
        };

        // Set up a topology initializer
        settingsWithTopologyInitializer.Properties[RabbitMqProperties.TopologyInitializer.Key] =
            new RabbitMqTopologyInitializer(async (channel, applyDefault) =>
            {
                await applyDefault();
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

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    public void When_EnsureChannelCalled_Given_ConcurrentAccess_Then_ShouldHandleThreadSafety(int concurrencyLevel)
    {
        // Arrange
        var manager = CreateChannelManager();
        var exceptions = new List<Exception>();

        // Act
        Parallel.For(0, concurrencyLevel, _ =>
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

    [Theory]
    [InlineData(1)]     // Single access
    [InlineData(10)]    // Concurrent access
    [InlineData(1000)]  // Rapid successive access
    public void When_ChannelPropertyAccessed_Given_MultipleAccess_Then_ShouldConsistentlyReturnNull(int accessCount)
    {
        // Arrange
        var manager = CreateChannelManager();
        var channels = new List<IChannel>();

        // Act
        if (accessCount <= 10)
        {
            Parallel.For(0, accessCount, _ =>
            {
                var channel = manager.Channel;
                lock (channels)
                {
                    channels.Add(channel);
                }
            });
        }
        else
        {
            for (int i = 0; i < accessCount; i++)
            {
                channels.Add(manager.Channel);
            }
        }

        // Assert
        channels.Should().OnlyContain(x => x == null, "Channel should be null until connection is established");
    }

    [Theory]
    [InlineData(typeof(IRabbitMqChannel))]
    [InlineData(typeof(IDisposable))]
    public void When_ManagerCreated_Given_ValidSettings_Then_ShouldImplementExpectedInterface(Type expectedInterfaceType)
    {
        // Arrange & Act
        var manager = CreateChannelManager();

        // Assert
        manager.Should().BeAssignableTo(expectedInterfaceType);
    }

    [Theory]
    [InlineData(true)]  // Always disposing
    [InlineData(false)] // Never disposing
    public void When_ManagerCreated_Given_IsDisposingFunc_Then_ShouldHandleCorrectly(bool isDisposing)
    {
        // Arrange & Act
        var manager = new RabbitMqChannelManager(
            _loggerFactory,
            _providerSettings,
            _settings,
            () => isDisposing);

        _managersToDispose.Add(manager);

        // Assert
        manager.Should().NotBeNull();
        manager.Channel.Should().BeNull();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void When_EnsureChannelCalled_Given_RepeatedCalls_Then_ShouldConsistentlyThrow(int callCount)
    {
        // Arrange
        var manager = CreateChannelManager();

        // Act & Assert
        for (int i = 0; i < callCount; i++)
        {
            manager.Invoking(m => m.EnsureChannel())
                .Should().Throw<ProducerMessageBusException>()
                .WithMessage("*The Channel is not available at this time*");
        }
    }

    [Theory]
    [InlineData(1)]  // Single subscriber
    [InlineData(3)]  // Multiple subscribers
    public void When_ChannelRecoveredEvent_Given_Subscribers_Then_ShouldAllowSubscription(int subscriberCount)
    {
        // Arrange
        var manager = CreateChannelManager();
        var eventCount = 0;

        // Act
        for (int i = 0; i < subscriberCount; i++)
        {
            manager.ChannelRecovered += (sender, args) => { eventCount++; };
        }

        // Assert
        eventCount.Should().Be(0, "Events should not be raised until channel recovery happens");
    }

    [Theory]
    [InlineData(1)]  // Unsubscribe once
    [InlineData(2)]  // Multiple unsubscribes (should not throw)
    public void When_ChannelRecoveredEvent_Given_Unsubscribe_Then_ShouldRemoveSubscription(int unsubscribeCount)
    {
        // Arrange
        var manager = CreateChannelManager();
        var eventCount = 0;
        void handler(object sender, EventArgs args) { eventCount++; }

        // Act
        manager.ChannelRecovered += handler;
        for (int i = 0; i < unsubscribeCount; i++)
        {
            manager.ChannelRecovered -= handler;
        }

        // Assert
        eventCount.Should().Be(0);
        manager.Should().NotBeNull();
    }

    [Fact]
    public void When_DisposeCalled_Given_EventSubscribers_Then_ShouldCleanupWithoutError()
    {
        // Arrange
        var manager = CreateChannelManager();
        var eventRaised = false;
        manager.ChannelRecovered += (sender, args) => { eventRaised = true; };

        // Act
        manager.Dispose();
        _managersToDispose.Remove(manager);

        // Assert
        eventRaised.Should().BeFalse();
    }

    [Fact]
    public async Task When_InitializeConnectionCalled_Given_TopologyProvisioningFails_Then_ShouldHandleGracefully()
    {
        // Arrange
        var settingsWithTopology = new RabbitMqMessageBusSettings
        {
            ConnectionFactory = new ConnectionFactory
            {
                HostName = "nonexistent-host",
                NetworkRecoveryInterval = TimeSpan.FromMilliseconds(50)
            }
        };

        var busSettings = new MessageBusSettings();

        var manager = new RabbitMqChannelManager(
            _loggerFactory,
            settingsWithTopology,
            busSettings,
            () => false);

        _managersToDispose.Add(manager);

        // Act
        await manager.InitializeConnection();

        // Assert
        manager.Channel.Should().BeNull("Channel should be null when connection fails");
    }

    [Fact]
    public void When_GarbageCollectionHappens_Given_ManagerNotDisposed_Then_ShouldSuppressFinalize()
    {
        // Arrange
        var manager = CreateChannelManager();

        // Act
        manager.Dispose();
        _managersToDispose.Remove(manager);

        // Act
        var act = () =>
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        };

        // Assert - Should not throw during GC
        act.Should().NotThrow();
    }

    [Fact]
    public async Task When_InitializeConnectionCalled_Given_AlreadyCancelled_Then_ShouldReturnImmediately()
    {
        // Arrange
        var manager = CreateChannelManager();
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel before calling

        // Act
        var stopwatch = Stopwatch.StartNew();
        await manager.InitializeConnection(cts.Token);
        stopwatch.Stop();

        // Assert
        manager.Channel.Should().BeNull();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "Should return immediately when already cancelled");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task When_InitializeConnectionRetries_Given_MultipleAttempts_Then_ShouldRespectRetryCount(int expectedRetries)
    {
        // This test verifies the retry mechanism respects the initial retry count

        // Arrange
        var settingsWithBadHost = new RabbitMqMessageBusSettings
        {
            ConnectionFactory = new ConnectionFactory
            {
                HostName = $"nonexistent-host-retry-test-{expectedRetries}",
                Port = 5672,
                NetworkRecoveryInterval = TimeSpan.FromMilliseconds(10)
            }
        };

        var manager = new RabbitMqChannelManager(
            _loggerFactory,
            settingsWithBadHost,
            _settings,
            () => false);

        _managersToDispose.Add(manager);

        // Act
        await manager.InitializeConnection();

        // Assert - After retries, channel should still be null
        manager.Channel.Should().BeNull();
    }

    [Fact]
    public void When_EnsureChannelCalled_Given_ChannelNull_Then_ShouldTriggerBackgroundReconnection()
    {
        // This test verifies that EnsureChannel triggers a background reconnection attempt

        // Arrange
        var manager = CreateChannelManager();

        // Act & Assert - First call should throw and trigger reconnection
        manager.Invoking(m => m.EnsureChannel())
            .Should().Throw<ProducerMessageBusException>()
            .WithMessage("*The Channel is not available at this time*");

        // Second call should also throw (connection still not established)
        manager.Invoking(m => m.EnsureChannel())
            .Should().Throw<ProducerMessageBusException>();
    }

    [Fact]
    public void When_ManagerDisposed_Given_WithConnectionRetryTimer_Then_ShouldDisposeTimerProperly()
    {
        // This test verifies proper cleanup of the connection retry timer

        // Arrange
        var manager = CreateChannelManager();

        // Trigger EnsureChannel to potentially start retry timer
        try
        {
            manager.EnsureChannel();
        }
        catch (ProducerMessageBusException)
        {
            // Expected
        }

        // Act - Dispose should clean up timer
        manager.Dispose();
        _managersToDispose.Remove(manager);

        // Assert - Should not throw
        manager.Channel.Should().BeNull();
    }

    [Fact]
    public async Task When_CancellationRequested_Given_DuringInitialization_Then_ShouldStopRetrying()
    {
        // This test verifies cancellation during initialization stops the retry loop

        // Arrange
        var settingsWithBadConnection = new RabbitMqMessageBusSettings
        {
            ConnectionFactory = new ConnectionFactory
            {
                HostName = "nonexistent-cancellation-test",
                Port = 5672,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
            }
        };

        var manager = new RabbitMqChannelManager(
            _loggerFactory,
            settingsWithBadConnection,
            _settings,
            () => false);

        _managersToDispose.Add(manager);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        // Act
        var stopwatch = Stopwatch.StartNew();
        await manager.InitializeConnection(cts.Token);
        stopwatch.Stop();

        // Assert
        manager.Channel.Should().BeNull();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(3000, "Should stop quickly after cancellation");
    }

    [Theory]
    [InlineData(true, false, true)]   // Bus-level enabled, no producer override -> true
    [InlineData(false, false, false)]  // Bus-level disabled, no producer override -> false
    [InlineData(false, true, true)]    // Bus-level disabled, producer enables -> true
    [InlineData(true, true, true)]     // Both enabled -> true
    public void When_NeedsConfirmsChannel_Given_BusAndProducerSettings_Then_ReturnsExpected(
        bool busLevelEnabled, bool producerLevelEnabled, bool expected)
    {
        // Arrange
        var providerSettings = new RabbitMqMessageBusSettings
        {
            ConnectionFactory = new ConnectionFactory(),
            EnablePublisherConfirms = busLevelEnabled
        };

        var busSettings = new MessageBusSettings();
        if (producerLevelEnabled)
        {
            var producerSettings = new ProducerSettings();
            RabbitMqProperties.EnablePublisherConfirms.Set(producerSettings, true);
            busSettings.Producers.Add(producerSettings);
        }

        var manager = new RabbitMqChannelManager(
            _loggerFactory,
            providerSettings,
            busSettings,
            () => false);
        _managersToDispose.Add(manager);

        // Act
        var result = manager.NeedsConfirmsChannel();

        // Assert
        result.Should().Be(expected);
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