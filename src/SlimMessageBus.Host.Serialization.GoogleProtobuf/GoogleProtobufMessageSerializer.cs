using System;
using System.Reflection;
using Google.Protobuf;
using Microsoft.Extensions.Logging;

namespace SlimMessageBus.Host.Serialization.GoogleProtobuf
{
    public class GoogleProtobufMessageSerializer : IMessageSerializer
    {
        private readonly ILogger _logger;
        private readonly IMessageParserFactory _messageParserFactory;

        public GoogleProtobufMessageSerializer(ILoggerFactory loggerFactory) : this(
        loggerFactory,
        new MessageParserFactory())
        {
        }

        public GoogleProtobufMessageSerializer(ILoggerFactory loggerFactory, IMessageParserFactory messageParserFactory)
        {
            _logger = loggerFactory.CreateLogger<GoogleProtobufMessageSerializer>();
            _messageParserFactory = messageParserFactory;
        }


        public byte[] Serialize(Type t, object message)
        {
            return ((IMessage) message).ToByteArray();
        }

        public object Deserialize(Type t, byte[] payload)
        {
            var messageParser = _messageParserFactory.CreateMessageParser(t);
            try
            {
                var message = messageParser.GetType()
                                           .InvokeMember("ParseFrom",
                                                         BindingFlags.InvokeMethod | BindingFlags.Public |
                                                         BindingFlags.Instance,
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