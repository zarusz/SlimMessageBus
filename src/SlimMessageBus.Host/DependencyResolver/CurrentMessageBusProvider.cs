namespace SlimMessageBus.Host;

using SlimMessageBus.Host.Consumer;

public class CurrentMessageBusProvider(IServiceProvider serviceProvider) : ICurrentMessageBusProvider
{
    public virtual IMessageBus GetCurrent()
    {
        var current = MessageScope.Current?.GetService<IMessageBus>();
        if (current is not null)
        {
            return current;
        }
        return serviceProvider.GetService<IMessageBus>();
    }
}
