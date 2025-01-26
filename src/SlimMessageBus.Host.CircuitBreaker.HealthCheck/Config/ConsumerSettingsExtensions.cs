namespace SlimMessageBus.Host.CircuitBreaker.HealthCheck;

static internal class ConsumerSettingsExtensions
{
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
        => consumerSettings.GetOrCreate(ConsumerSettingsProperties.HealthStatusTags, () => []);
}
