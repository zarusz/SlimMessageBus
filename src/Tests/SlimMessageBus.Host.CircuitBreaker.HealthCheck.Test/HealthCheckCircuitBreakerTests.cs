namespace SlimMessageBus.Host.CircuitBreaker.HealthCheck.Test;
public class HealthCheckCircuitBreakerTests
{
    private readonly Mock<IHealthCheckHostBreaker> _hostMock;
    private readonly HealthCheckCircuitBreaker _circuitBreaker;
    private readonly TestConsumerSettings _testConsumerSettings;
    private readonly TestConsumerSettings _testConsumerSettings2;

    public HealthCheckCircuitBreakerTests()
    {
        _testConsumerSettings = new TestConsumerSettings();
        _testConsumerSettings2 = new TestConsumerSettings();

        _hostMock = new Mock<IHealthCheckHostBreaker>();
        var _settings = new List<AbstractConsumerSettings>
        {
            _testConsumerSettings,
            _testConsumerSettings2
        };

        _circuitBreaker = new HealthCheckCircuitBreaker(
            _settings,
            _hostMock.Object);
    }

    [Fact]
    public void Constructor_ShouldInitializeOpenState()
    {
        // assert
        _circuitBreaker.State.Should().Be(Circuit.Open);
    }

    [Fact]
    public async Task Subscribe_ShouldSetOnChangeAndSubscribeToHost()
    {
        // arrange
        static Task onChange(Circuit _) => Task.CompletedTask;

        // act
        await _circuitBreaker.Subscribe(onChange);

        // assert
        _hostMock.Verify(h => h.Subscribe(It.IsAny<OnChangeDelegate>()), Times.Once);
        _circuitBreaker.State.Should().Be(Circuit.Open);
    }

    [Fact]
    public async Task Subscribe_ShouldMergeTagsFromAllSettings()
    {
        const string degradedTag = "degraded";
        const string unhealthyTag = "unhealthy";

        _testConsumerSettings.PauseOnUnhealthy(unhealthyTag);
        _testConsumerSettings2.PauseOnDegraded(degradedTag);

        var expected = new Dictionary<string, HealthStatus>()
        {
            { unhealthyTag, HealthStatus.Unhealthy },
            { degradedTag, HealthStatus.Degraded }
        };

        // arrange
        static Task onChange(Circuit _) => Task.CompletedTask;
        await _circuitBreaker.Subscribe(onChange);

        // act
        var actual = typeof(HealthCheckCircuitBreaker)
            .GetField("_monitoredTags", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(_circuitBreaker) as IDictionary<string, HealthStatus>;

        // assert
        actual.Should().NotBeNull();
        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Subscribe_MergedTagsOfDifferentSeverity_ShouldUseLeastSevereCondition()
    {
        const string tag = "tag";
        _testConsumerSettings.PauseOnUnhealthy(tag);
        _testConsumerSettings2.PauseOnDegraded(tag);

        var expected = new Dictionary<string, HealthStatus>()
        {
            { tag, HealthStatus.Degraded },
        };

        // arrange
        static Task onChange(Circuit _) => Task.CompletedTask;
        await _circuitBreaker.Subscribe(onChange);

        // act
        var actual = typeof(HealthCheckCircuitBreaker)
            .GetField("_monitoredTags", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(_circuitBreaker) as IDictionary<string, HealthStatus>;

        // assert
        actual.Should().NotBeNull();
        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task TagStatusChanged_ShouldChangeStateToClosed_WhenUnhealthyTagIsUnhealthy()
    {
        // arrange
        const string tag = "tag";
        _testConsumerSettings.PauseOnUnhealthy(tag);

        static Task onChange(Circuit _) => Task.CompletedTask;
        await _circuitBreaker.Subscribe(onChange);

        var tags = new Dictionary<string, HealthStatus>
        {
            { tag, HealthStatus.Unhealthy }
        };

        // act
        await _circuitBreaker.TagStatusChanged(tags);

        // assert
        _circuitBreaker.State.Should().Be(Circuit.Closed);
    }

    [Fact]
    public async Task TagStatusChanged_ShouldChangeStateToClosed_WhenDegradedTagIsUnhealthy()
    {
        // arrange
        const string tag = "tag";
        _testConsumerSettings.PauseOnDegraded(tag);

        static Task onChange(Circuit _) => Task.CompletedTask;
        await _circuitBreaker.Subscribe(onChange);

        var tags = new Dictionary<string, HealthStatus>
        {
            { tag, HealthStatus.Unhealthy }
        };

        // act
        await _circuitBreaker.TagStatusChanged(tags);

        // assert
        _circuitBreaker.State.Should().Be(Circuit.Closed);
    }

    [Fact]
    public async Task TagStatusChanged_ShouldRemainOpen_WhenUnmonitoredTagsAreUnhealthyOrDegraded()
    {
        // arrange
        _testConsumerSettings.PauseOnUnhealthy("tag1", "tag2");

        Func<Circuit, Task> onChange = _ => Task.CompletedTask;
        await _circuitBreaker.Subscribe(onChange);

        var tags = new Dictionary<string, HealthStatus>
        {
            { "tag1", HealthStatus.Healthy },
            { "tag2", HealthStatus.Degraded },
            { "unmonitored1", HealthStatus.Unhealthy },
            { "unmonitored2", HealthStatus.Degraded }
        };

        // act
        await _circuitBreaker.TagStatusChanged(tags);

        // assert
        _circuitBreaker.State.Should().Be(Circuit.Open);
    }

    [Fact]
    public void Unsubscribe_ShouldUnsubscribeFromHostAndClearOnChange()
    {
        // act
        _circuitBreaker.Unsubscribe();

        // assert
        _hostMock.Verify(h => h.Unsubscribe(It.IsAny<OnChangeDelegate>()), Times.Once);
        _circuitBreaker.State.Should().Be(Circuit.Open);
    }

    public class TestConsumerSettings : AbstractConsumerSettings
    {
    }
}
