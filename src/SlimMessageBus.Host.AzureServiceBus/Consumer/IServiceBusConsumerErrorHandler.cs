namespace SlimMessageBus.Host.AzureServiceBus;

public interface IServiceBusConsumerErrorHandler<in T> : IConsumerErrorHandler<T>;

public abstract class ServiceBusConsumerErrorHandler<T> : ConsumerErrorHandler<T>;