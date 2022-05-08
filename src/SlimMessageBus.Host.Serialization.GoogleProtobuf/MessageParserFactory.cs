using System;
using System.Linq.Expressions;
using Google.Protobuf;

namespace SlimMessageBus.Host.Serialization.GoogleProtobuf
{
    public class MessageParserFactory : IMessageParserFactory
    {
        public object CreateMessageParser(Type messageType)
        {
            var parserType = typeof(MessageParser<>).MakeGenericType(messageType);
            var constructor = messageType.GetConstructor(Type.EmptyTypes);
            if (constructor == null)
            {
                throw new InvalidOperationException(
                "The type MessageParser<> does not have a parameterless constructor");
            }

            var callConstructor = Expression.New(constructor);
            var cast = Expression.Convert(callConstructor, messageType);
            var function = Expression.Lambda(cast).Compile();
            var parser = Activator.CreateInstance(parserType,
                                                  function);
            return parser;
        }
    }
}