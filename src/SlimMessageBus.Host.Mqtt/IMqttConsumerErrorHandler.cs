namespace SlimMessageBus.Host.Mqtt;

public interface IMqttConsumerErrorHandler<in T> : IConsumerErrorHandler<T>;

public abstract class MqttConsumerErrorHandler<T> : ConsumerErrorHandler<T>, IMqttConsumerErrorHandler<T>;