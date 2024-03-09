namespace SlimMessageBus.Host;

using SlimMessageBus.Host.Consumer;

public interface IMessageScopeFactory
{
    IMessageScope CreateMessageScope(ConsumerSettings consumerSettings, object message, IDictionary<string, object> consumerContextProperties, IServiceProvider currentServiceProvider);
}
