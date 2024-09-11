namespace SlimMessageBus.Host.Kafka;

public interface IKafkaConsumerErrorHandler<in T> : IConsumerErrorHandler<T>
{
}