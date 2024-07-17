namespace SlimMessageBus.Host.Test.Consumer;

public class AbstractConsumerTests
{
    private class TestConsumer : AbstractConsumer
    {
        public TestConsumer(ILogger logger, IEnumerable<AbstractConsumerSettings> settings)
            : base(logger, settings) { }

        protected override Task OnStart() => Task.CompletedTask;
        protected override Task OnStop() => Task.CompletedTask;
    }

    private class TestConsumerSettings : AbstractConsumerSettings;

    public class CircuitBreakerAccessor
    {
        public Circuit State { get; set; }
        public int SubscribeCallCount { get; set; } = 0;
        public int UnsubscribeCallCount { get; set; } = 0;
        public IEnumerable<AbstractConsumerSettings> Settings { get; set; }
        public Func<Circuit, Task> OnChange { get; set; }
    }

    private class TestCircuitBreaker : IConsumerCircuitBreaker
    {
        private readonly CircuitBreakerAccessor _accessor;

        public TestCircuitBreaker(CircuitBreakerAccessor accessor, IEnumerable<AbstractConsumerSettings> settings)
        {
            _accessor = accessor;
            Settings = settings;
            State = Circuit.Open;
        }

        public Circuit State
        {
            get => _accessor.State;
            set => _accessor.State = value;
        }
        public IEnumerable<AbstractConsumerSettings> Settings { get; }

        public Task Subscribe(Func<Circuit, Task> onChange)
        {
            _accessor.SubscribeCallCount++;
            _accessor.OnChange = onChange;

            return Task.CompletedTask;
        }

        public void Unsubscribe()
        {
            _accessor.UnsubscribeCallCount++;
        }
    }

    private readonly List<AbstractConsumerSettings> _settings;
    private readonly TestConsumer _target;
    private readonly CircuitBreakerAccessor accessor;

    public AbstractConsumerTests()
    {
        accessor = new CircuitBreakerAccessor();

        var serviceCollection = new ServiceCollection();
        serviceCollection.TryAddSingleton(accessor);
        serviceCollection.TryAddTransient<TestCircuitBreaker>();

        var testSettings = new TestConsumerSettings
        {
            MessageBusSettings = new MessageBusSettings { ServiceProvider = serviceCollection.BuildServiceProvider() }
        };

        testSettings.CircuitBreakers.Add<TestCircuitBreaker>();

        _settings = [testSettings];

        _target = new TestConsumer(NullLogger.Instance, _settings);
    }

    [Fact]
    public async Task Start_ShouldStartCircuitBreakers_WhenNotStarted()
    {
        // Arrange

        // Act
        await _target.Start();

        // Assert
        _target.IsStarted.Should().BeTrue();
        accessor.SubscribeCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Stop_ShouldStopCircuitBreakers_WhenStarted()
    {
        // Arrange
        await _target.Start();

        // Act
        await _target.Stop();

        // Assert
        _target.IsStarted.Should().BeFalse();
        accessor.UnsubscribeCallCount.Should().Be(1);
    }

    [Fact]
    public async Task BreakerChanged_ShouldPauseConsumer_WhenBreakerClosed()
    {
        // Arrange
        await _target.Start();

        // Act
        _target.IsPaused.Should().BeFalse();
        accessor.State = Circuit.Closed;
        await _target.BreakerChanged(Circuit.Closed);

        // Assert
        _target.IsPaused.Should().BeTrue();
    }

    [Fact]
    public async Task BreakerChanged_ShouldResumeConsumer_WhenBreakerOpen()
    {
        // Arrange
        await _target.Start();
        accessor.State = Circuit.Closed;
        await _target.BreakerChanged(Circuit.Open);

        // Act
        _target.IsPaused.Should().BeTrue();
        accessor.State = Circuit.Open;
        await _target.BreakerChanged(Circuit.Open);

        // Assert
        _target.IsPaused.Should().BeFalse();
    }
}
