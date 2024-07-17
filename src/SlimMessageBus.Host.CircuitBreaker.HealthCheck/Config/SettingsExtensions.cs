namespace SlimMessageBus.Host.CircuitBreaker.HealthCheck;

static internal class SettingsExtensions
{
    private const string _key = nameof(HealthCheckCircuitBreaker);

    public static T PauseOnDegraded<T>(this T consumerSettings, params string[] tags)
        where T : AbstractConsumerSettings
    {
        if (tags.Length > 0)
        {
            var dict = consumerSettings.HealthBreakerTags();
            foreach (var tag in tags)
            {
                dict[tag] = HealthStatus.Degraded;
            }
        }

        return consumerSettings;
    }

    public static T PauseOnUnhealthy<T>(this T consumerSettings, params string[] tags)
        where T : AbstractConsumerSettings
    {
        if (tags.Length > 0)
        {
            var dict = consumerSettings.HealthBreakerTags();
            foreach (var tag in tags)
            {
                dict[tag] = HealthStatus.Unhealthy;
            }
        }

        return consumerSettings;
    }

    static internal IDictionary<string, HealthStatus> HealthBreakerTags(this AbstractConsumerSettings consumerSettings)
    {
        if (!consumerSettings.Properties.TryGetValue(_key, out var rawValue) || rawValue is not IDictionary<string, HealthStatus> value)
        {
            value = new Dictionary<string, HealthStatus>();
            consumerSettings.Properties[_key] = value;
        }

        return value;
    }
}
