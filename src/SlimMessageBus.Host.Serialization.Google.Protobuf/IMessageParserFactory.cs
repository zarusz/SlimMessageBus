using System;
using Google.Protobuf;

namespace SlimMessageBus.Host.Serialization.Google.Protobuf
{
    public interface IMessageParserFactory
    {
        object CreateMessageParser(Type messageType);
    }
}