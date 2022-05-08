using System;

namespace SlimMessageBus.Host.Serialization.GoogleProtobuf
{
    public interface IMessageParserFactory
    {
        object CreateMessageParser(Type messageType);
    }
}