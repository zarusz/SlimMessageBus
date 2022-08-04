namespace SlimMessageBus.Host.AzureServiceBus;

using SlimMessageBus.Host.Config;

public class ConsumerSessionBuilder
{
    internal ConsumerSettings ConsumerSettings { get; }

    public ConsumerSessionBuilder(ConsumerSettings consumerSettings) => ConsumerSettings = consumerSettings;

    /// <summary>
    /// Sets the Azue Service Bus session idle timeout.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder"></param>
    /// <param name="enable"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public ConsumerSessionBuilder SessionIdleTimeout(TimeSpan idleTimeout)
    {
        ConsumerSettings.SetSessionIdleTimeout(idleTimeout);

        return this;
    }

    /// <summary>
    /// Sets the Azue Service Bus maximmum concurrent sessions that can be handled by this consumer.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder"></param>
    /// <param name="enable"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public ConsumerSessionBuilder MaxConcurrentSessions(int maxConcurrentSessions)
    {
        ConsumerSettings.SetMaxConcurrentSessions(maxConcurrentSessions);

        return this;
    }
}