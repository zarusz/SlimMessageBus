namespace SlimMessageBus.Host;

public interface ICurrentMessageBusProvider
{
    IMessageBus GetCurrent();
}
