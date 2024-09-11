namespace SlimMessageBus.Host.AzureEventHub;

public interface IEventHubConsumerErrorHandler<in T> : IConsumerErrorHandler<T>
{
}