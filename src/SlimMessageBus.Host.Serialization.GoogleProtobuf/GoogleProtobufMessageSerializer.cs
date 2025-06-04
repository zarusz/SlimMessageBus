namespace SlimMessageBus.Host.Serialization.GoogleProtobuf;

using System.Collections.Generic;
using System.Reflection;

using Google.Protobuf;

public class GoogleProtobufMessageSerializer : IMessageSerializer, IMessageSerializerProvider
{
    private readonly ILogger _logger;
    private readonly IMessageParserFactory _messageParserFactory;

    public GoogleProtobufMessageSerializer(ILoggerFactory loggerFactory, IMessageParserFactory messageParserFactory = null)
    {
        _logger = loggerFactory.CreateLogger<GoogleProtobufMessageSerializer>();
        _messageParserFactory = messageParserFactory ?? new MessageParserFactory();
    }

    public byte[] Serialize(Type messageType, IDictionary<string, object> headers, object message, object transportMessage)
        => ((IMessage)message).ToByteArray();

    public object Deserialize(Type messageType, IReadOnlyDictionary<string, object> headers, byte[] payload, object transportMessage)
    {
        var messageParser = _messageParserFactory.CreateMessageParser(messageType);
        try
        {
            var message = messageParser.GetType()
                                       .InvokeMember("ParseFrom",
                                                     BindingFlags.InvokeMethod | BindingFlags.Public |
                                                     BindingFlags.Instance,
                                                     null,
                                                     messageParser,
                                                     [payload]);

            return message;
        }
        catch (TargetInvocationException exception)
        {
            _logger.LogWarning(exception, "Failed to call 'ParseFrom' of type [{typename}]", messageType.FullName);

            throw exception.InnerException ?? exception;
        }
    }

    public IMessageSerializer GetSerializer(string path) => this;
}