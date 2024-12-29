namespace SlimMessageBus.Host.Kafka;

public interface IKafkaConsumerErrorHandler<in T> : IConsumerErrorHandler<T>;

public abstract class KafkaConsumerErrorHandler<T> : ConsumerErrorHandler<T>;