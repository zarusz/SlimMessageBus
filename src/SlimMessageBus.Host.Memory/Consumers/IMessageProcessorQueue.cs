namespace SlimMessageBus.Host.Memory;

public interface IMessageProcessorQueue
{
    void Enqueue(object transportMessage, IReadOnlyDictionary<string, object> messageHeaders);
}
