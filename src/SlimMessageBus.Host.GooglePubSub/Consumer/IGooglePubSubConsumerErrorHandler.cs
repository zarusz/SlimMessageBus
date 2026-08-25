namespace SlimMessageBus.Host.GooglePubSub;

public interface IGooglePubSubConsumerErrorHandler<in T> : IConsumerErrorHandler<T>;

public abstract class GooglePubSubConsumerErrorHandler<T>
    : ConsumerErrorHandler<T>, IGooglePubSubConsumerErrorHandler<T>;
