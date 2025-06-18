namespace SlimMessageBus.Host.CircuitBreaker.Test;

public class ConsumerBuilderExtensionsTests
{
    private class DummyCircuitBreaker : IConsumerCircuitBreaker
    {
        public Circuit State => throw new NotImplementedException();

        public Task Subscribe(Func<Circuit, Task> onChange) => throw new NotImplementedException();

        public void Unsubscribe() => throw new NotImplementedException();
    }

    private class TestConsumerBuilder() : AbstractConsumerBuilder(new MessageBusSettings(), typeof(string), null)
    {
    }

    [Fact]
    public void When_AddConsumerCircuitBreakerType_Called_With_Null_Builder_Given_No_Builder_Then_Throws_ArgumentNullException()
    {
        // Arrange
        TestConsumerBuilder builder = null!;

        // Act
        Action act = () => ConsumerBuilderExtensions.AddConsumerCircuitBreakerType<TestConsumerBuilder, DummyCircuitBreaker>(builder);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    [Fact]
    public void When_AddConsumerCircuitBreakerType_Called_Given_Valid_Builder_Then_Registers_BreakerType_And_Interceptor()
    {
        // Arrange
        var builder = new TestConsumerBuilder();

        // Act
        var result = builder.AddConsumerCircuitBreakerType<TestConsumerBuilder, DummyCircuitBreaker>();

        // Assert: returns same builder
        result.Should().BeSameAs(builder);

        // Assert: breaker type is registered in ConsumerSettings
        var types = builder.ConsumerSettings.GetOrCreate(ConsumerSettingsProperties.CircuitBreakerTypes, () => []);
        types.Should().Contain(typeof(DummyCircuitBreaker));

        // Assert: PostConfigurationActions registers the interceptor
        var services = new ServiceCollection();
        foreach (var action in builder.PostConfigurationActions)
        {
            action(services);
        }
        services.Should().ContainSingle(sd =>
            sd.ServiceType == typeof(IAbstractConsumerInterceptor) &&
            sd.ImplementationType == typeof(CircuitBreakerConsumerInterceptor) &&
            sd.Lifetime == ServiceLifetime.Singleton);
    }
}
