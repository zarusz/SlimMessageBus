namespace SlimMessageBus.Host.CircuitBreaker.Test;

public class CircuitBreakerConsumerInterceptor_SharedBreakerTests
{
    [Fact]
    public void When_TwoConsumersShareSameHealthCheck_Then_ShouldShareSameCircuitBreakerInstance()
    {
        // This test verifies the fix for the issue where multiple consumers on the same queue
        // with the same health check would each get their own circuit breaker instance,
        // causing only one consumer to pause when the health check failed.
        
        // The fix ensures that consumers monitoring the same health checks share a single
        // circuit breaker instance, so when it trips, ALL consumers using it are paused.
        
        // Arrange
        var settings1 = new ConsumerSettings
        {
            MessageType = typeof(string),
            Path = "test-queue"
        };
        settings1.Properties["CircuitBreaker_HealthStatusTags"] = new Dictionary<string, int>
        {
            { "HMRC_CDS", 2 } // HealthStatus.Degraded = 2
        };

        var settings2 = new ConsumerSettings
        {
            MessageType = typeof(int),
            Path = "test-queue"
        };
        settings2.Properties["CircuitBreaker_HealthStatusTags"] = new Dictionary<string, int>
        {
            { "HMRC_CDS", 2 } // Same health check, same threshold
        };

        // Act - Get breaker keys for both
        var key1 = GetBreakerKeyViaReflection(typeof(object), [settings1]);
        var key2 = GetBreakerKeyViaReflection(typeof(object), [settings2]);

        // Assert
        key1.Should().Be(key2, 
            "Consumers monitoring the same health check should generate the same breaker key, " +
            "ensuring they share the same circuit breaker instance");
    }

    [Fact]
    public void When_TwoConsumersHaveDifferentHealthChecks_Then_ShouldHaveDifferentCircuitBreakerInstances()
    {
        // Arrange
        var settings1 = new ConsumerSettings
        {
            MessageType = typeof(string),
            Path = "test-queue"
        };
        settings1.Properties["CircuitBreaker_HealthStatusTags"] = new Dictionary<string, int>
        {
            { "HMRC_CDS", 2 } // HealthStatus.Degraded = 2
        };

        var settings2 = new ConsumerSettings
        {
            MessageType = typeof(int),
            Path = "test-queue"
        };
        settings2.Properties["CircuitBreaker_HealthStatusTags"] = new Dictionary<string, int>
        {
            { "Database", 2 } // Different health check
        };

        // Act
        var key1 = GetBreakerKeyViaReflection(typeof(object), [settings1]);
        var key2 = GetBreakerKeyViaReflection(typeof(object), [settings2]);

        // Assert
        key1.Should().NotBe(key2,
            "Consumers monitoring different health checks should have different circuit breaker instances");
    }

    [Fact]
    public void When_TwoConsumersHaveSameHealthCheckButDifferentThresholds_Then_ShouldHaveDifferentCircuitBreakerInstances()
    {
        // Arrange
        var settings1 = new ConsumerSettings
        {
            MessageType = typeof(string),
            Path = "test-queue"
        };
        settings1.Properties["CircuitBreaker_HealthStatusTags"] = new Dictionary<string, int>
        {
            { "HMRC_CDS", 2 } // HealthStatus.Degraded = 2
        };

        var settings2 = new ConsumerSettings
        {
            MessageType = typeof(int),
            Path = "test-queue"
        };
        settings2.Properties["CircuitBreaker_HealthStatusTags"] = new Dictionary<string, int>
        {
            { "HMRC_CDS", 1 } // HealthStatus.Unhealthy = 1 - different threshold
        };

        // Act
        var key1 = GetBreakerKeyViaReflection(typeof(object), [settings1]);
        var key2 = GetBreakerKeyViaReflection(typeof(object), [settings2]);

        // Assert
        key1.Should().NotBe(key2,
            "Consumers with the same health check but different thresholds should have different circuit breaker instances");
    }

    private string GetBreakerKeyViaReflection(Type breakerType, IEnumerable<AbstractConsumerSettings> consumerSettings)
    {
        var interceptorType = typeof(CircuitBreakerConsumerInterceptor);
        var method = interceptorType.GetMethod("GetBreakerKey",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        return (string)method!.Invoke(null, [breakerType, consumerSettings])!;
    }
}
