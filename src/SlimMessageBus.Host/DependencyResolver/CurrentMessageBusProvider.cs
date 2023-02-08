namespace SlimMessageBus.Host;

using Microsoft.Extensions.DependencyInjection;

using SlimMessageBus;
using SlimMessageBus.Host.Consumer;

public class CurrentMessageBusProvider : ICurrentMessageBusProvider
{
    private readonly IServiceProvider _serviceProvider;

    public CurrentMessageBusProvider(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    public virtual IMessageBus GetCurrent()
    {
        var current = MessageScope.Current?.GetService<IMessageBus>();
        if (current is not null)
        {
            return current;
        }

        return _serviceProvider.GetService<IMessageBus>();
    }
}
