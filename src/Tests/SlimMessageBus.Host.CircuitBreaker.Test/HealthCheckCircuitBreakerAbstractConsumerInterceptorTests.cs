namespace SlimMessageBus.Host.CircuitBreaker.Test;

public class CircuitBreakerAbstractConsumerInterceptorTests
{
    private class TestConsumer(ILogger logger, IEnumerable<AbstractConsumerSettings> settings, IEnumerable<IAbstractConsumerInterceptor> interceptors)
        : AbstractConsumer(logger, settings, "path", interceptors)
    {
        protected override Task OnStart() => Task.CompletedTask;
        protected override Task OnStop() => Task.CompletedTask;
    }

    private class TestConsumerSettings : AbstractConsumerSettings;

    public class CircuitBreakerAccessor
    {
        public Circuit State { get; set; }
        public int SubscribeCallCount { get; set; } = 0;
        public int UnsubscribeCallCount { get; set; } = 0;
        public Func<Circuit, Task>? OnChange { get; set; }
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

    public CircuitBreakerAbstractConsumerInterceptorTests()
    {
        accessor = new CircuitBreakerAccessor();

        var h = new CircuitBreakerConsumerInterceptor(NullLogger<CircuitBreakerConsumerInterceptor>.Instance);

        var serviceCollection = new ServiceCollection();
        serviceCollection.TryAddSingleton(accessor);
        serviceCollection.TryAddTransient<TestCircuitBreaker>();
        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<IAbstractConsumerInterceptor>(h));

        var testSettings = new TestConsumerSettings
        {
            MessageBusSettings = new MessageBusSettings { ServiceProvider = serviceCollection.BuildServiceProvider() }
        };

        var breakers = testSettings.GetOrCreate(ConsumerSettingsProperties.CircuitBreakerTypes, () => []);
        breakers.Add<TestCircuitBreaker>();

        _settings = [testSettings];

        _target = new TestConsumer(NullLogger.Instance, _settings, [h]);
    }

    [Fact]
    public async Task When_Start_ShouldStartCircuitBreakers_WhenNotStarted()
    {
        // Arrange

        // Act
        await _target.Start();

        // Assert
        _target.IsStarted.Should().BeTrue();
        accessor.SubscribeCallCount.Should().Be(1);
    }

    [Fact]
    public async Task When_Stop_ShouldStopCircuitBreakers_WhenStarted()
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
    public async Task When_BreakerChanged_Should_PauseConsumer_Given_BreakerClosed()
    {
        // Arrange
        await _target.Start();

        // Act
        _target.GetOrDefault(AbstractConsumerProperties.IsPaused, false).Should().BeFalse();
        accessor.State = Circuit.Closed;
        await accessor.OnChange!(Circuit.Closed);

        // Assert
        _target.GetOrDefault(AbstractConsumerProperties.IsPaused, false).Should().BeTrue();
    }

    [Fact]
    public async Task When_BreakerChanged_Should_ResumeConsumer_Given_BreakerOpen()
    {
        // Arrange
        await _target.Start();
        accessor.State = Circuit.Closed;
        await accessor.OnChange!(Circuit.Open);

        // Act
        _target.GetOrDefault(AbstractConsumerProperties.IsPaused, false).Should().BeTrue();
        accessor.State = Circuit.Open;
        await accessor.OnChange(Circuit.Open);

        // Assert
        _target.GetOrDefault(AbstractConsumerProperties.IsPaused, false).Should().BeFalse();
    }
}
