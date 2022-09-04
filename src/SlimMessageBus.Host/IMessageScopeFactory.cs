namespace SlimMessageBus.Host;

using SlimMessageBus.Host.Config;

public interface IMessageScopeFactory
{
    MessageScopeWrapper CreateMessageScope(ConsumerSettings consumerSettings, object message);
}
