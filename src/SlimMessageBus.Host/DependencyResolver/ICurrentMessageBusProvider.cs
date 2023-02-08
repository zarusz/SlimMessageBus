namespace SlimMessageBus.Host;

using SlimMessageBus;

public interface ICurrentMessageBusProvider
{
    public IMessageBus GetCurrent();
}

