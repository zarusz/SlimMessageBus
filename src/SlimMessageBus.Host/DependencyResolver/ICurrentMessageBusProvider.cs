namespace SlimMessageBus.Host;

public interface ICurrentMessageBusProvider
{
    public IMessageBus GetCurrent();
}

