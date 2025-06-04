namespace SlimMessageBus.Host;

public interface IMessageHeadersFactory
{
    Dictionary<string, object> CreateHeaders();
}