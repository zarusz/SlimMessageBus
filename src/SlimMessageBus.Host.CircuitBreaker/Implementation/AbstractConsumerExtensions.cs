namespace SlimMessageBus.Host.CircuitBreaker;

internal static class AbstractConsumerExtensions
{
    public static bool IsPaused(this AbstractConsumer consumer) => consumer.GetOrDefault(AbstractConsumerProperties.IsPaused, false);
    public static void SetIsPaused(this AbstractConsumer consumer, bool isPaused) => AbstractConsumerProperties.IsPaused.Set(consumer, isPaused);
}