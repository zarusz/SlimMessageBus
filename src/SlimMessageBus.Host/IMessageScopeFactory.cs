namespace SlimMessageBus.Host;

using SlimMessageBus.Host.Consumer;

public interface IMessageScopeFactory
{
    MessageScopeWrapper CreateMessageScope(ConsumerSettings consumerSettings, object message, IServiceProvider currentServiceProvider);
}
