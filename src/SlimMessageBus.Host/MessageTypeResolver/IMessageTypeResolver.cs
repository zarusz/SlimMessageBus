namespace SlimMessageBus.Host;

public interface IMessageTypeResolver
{
    string ToName(Type messageType);
    Type ToType(string name);
}