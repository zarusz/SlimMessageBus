using System;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using Google.Protobuf;
using Microsoft.Extensions.Logging;

namespace SlimMessageBus.Host.Serialization.Google.Protobuf
{
    public class GoogleProtobufMessageSerializer : IMessageSerializer
    {
        private readonly ILogger _logger;

       
        public GoogleProtobufMessageSerializer(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<GoogleProtobufMessageSerializer>();
        }

        public byte[] Serialize(Type t, object message)
        {
            return ((IMessage)message).ToByteArray();
        }

        private static object GenerateParserObject(Type t)
        {
            var parserType = typeof(MessageParser<>).MakeGenericType(t);

            var constructor = t.GetConstructor(Type.EmptyTypes);
            if (constructor == null)
            {
                throw new InvalidOperationException($"The type MessageParser<> does not have a parameterless constructor");
            }

            var callConstructor = Expression.New(constructor);
            var cast = Expression.Convert(callConstructor, t);
            var function = Expression.Lambda(cast).Compile();


            var parser = Activator.CreateInstance(parserType,
                new object[]
                {
                    function
                });
            return parser;
        }


        public object Deserialize(Type t, byte[] payload)
        {
            var messageParser = GenerateParserObject(t);


            try
            {
                var message = messageParser.GetType()
                    .InvokeMember("ParseFrom",
                        BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance,
                        null,
                        messageParser,
                        new object[] { payload });

                return message;
            }
            catch (TargetInvocationException exception)
            {
                _logger.LogWarning(exception, "Failed to call 'ParseFrom' of type [{typename}]", t.FullName);

                throw exception.InnerException ?? exception;
            }
        }
    }
}