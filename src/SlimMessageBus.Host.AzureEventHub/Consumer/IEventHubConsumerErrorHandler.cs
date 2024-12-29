namespace SlimMessageBus.Host.AzureEventHub;

public interface IEventHubConsumerErrorHandler<in T> : IConsumerErrorHandler<T>;

public abstract class EventHubConsumerErrorHandler<T> : ConsumerErrorHandler<T>;