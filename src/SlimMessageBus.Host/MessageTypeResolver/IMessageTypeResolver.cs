namespace SlimMessageBus.Host
{
    using System;

    public interface IMessageTypeResolver
    {
        string ToName(Type messageType);
        Type ToType(string name);
    }
}