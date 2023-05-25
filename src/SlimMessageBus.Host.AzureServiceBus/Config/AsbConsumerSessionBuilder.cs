namespace SlimMessageBus.Host.AzureServiceBus;

public class AsbConsumerSessionBuilder
{
    internal AbstractConsumerSettings ConsumerSettings { get; }

    public AsbConsumerSessionBuilder(AbstractConsumerSettings consumerSettings) => ConsumerSettings = consumerSettings;

    /// <summary>
    /// Sets the Azue Service Bus session idle timeout.
    /// </summary>
    /// <param name="sessionIdleTimeout"></param>
    /// <returns></returns>
    public AsbConsumerSessionBuilder SessionIdleTimeout(TimeSpan sessionIdleTimeout)
    {
        ConsumerSettings.SetSessionIdleTimeout(sessionIdleTimeout);

        return this;
    }

    /// <summary>
    /// Sets the Azue Service Bus maximmum concurrent sessions that can be handled by this consumer.
    /// </summary>
    /// <param name="maxConcurrentSessions"></param>
    /// <returns></returns>
    public AsbConsumerSessionBuilder MaxConcurrentSessions(int maxConcurrentSessions)
    {
        ConsumerSettings.SetMaxConcurrentSessions(maxConcurrentSessions);

        return this;
    }
}