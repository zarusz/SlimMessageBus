namespace SlimMessageBus.Host.Serialization.GoogleProtobuf;

using System.Reflection;

using Google.Protobuf;

public class GoogleProtobufMessageSerializer : IMessageSerializer
{
    private readonly ILogger _logger;
    private readonly IMessageParserFactory _messageParserFactory;

    public GoogleProtobufMessageSerializer(ILoggerFactory loggerFactory, IMessageParserFactory messageParserFactory = null)
    {
        _logger = loggerFactory.CreateLogger<GoogleProtobufMessageSerializer>();
        _messageParserFactory = messageParserFactory ?? new MessageParserFactory();
    }

    public byte[] Serialize(Type t, object message)
        => ((IMessage)message).ToByteArray();

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
                                                     [payload]);

            return message;
        }
        catch (TargetInvocationException exception)
        {
            _logger.LogWarning(exception, "Failed to call 'ParseFrom' of type [{typename}]", t.FullName);

            throw exception.InnerException ?? exception;
        }
    }
}