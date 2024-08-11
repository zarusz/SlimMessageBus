namespace SlimMessageBus.Host.Consumer;

internal sealed class MessageScopeAccessor : IMessageScopeAccessor
{
    public IServiceProvider Current => MessageScope.Current;
}