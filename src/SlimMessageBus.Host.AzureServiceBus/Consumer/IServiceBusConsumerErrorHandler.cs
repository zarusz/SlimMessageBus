namespace SlimMessageBus.Host.AzureServiceBus;

public interface IServiceBusConsumerErrorHandler<in T> : IConsumerErrorHandler<T>
{
}