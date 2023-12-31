namespace SlimMessageBus.Host;

public interface IMessageBusProvider
{
    string Name { get; }
    MessageBusSettings Settings { get; }
}