namespace SlimMessageBus.Host;

public interface IMessageHeadersFactory
{
    IDictionary<string, object> CreateHeaders();
}