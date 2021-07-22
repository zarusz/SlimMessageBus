namespace SlimMessageBus.Host
{
    using System;

    public interface IMessageTypeResolver
    {
        string ToName(Type message);
        Type ToType(string name);
    }
}