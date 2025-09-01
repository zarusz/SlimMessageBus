namespace SlimMessageBus.Host;

public static class IMessageSerializerExtensions
{
    public static MessageProvider<TTransportMessage> GetMessageProvider<TPayload, TTransportMessage>(this IMessageSerializer<TPayload> messageSerializer, Func<TTransportMessage, TPayload> payloadProvider) => (Type messageType, IReadOnlyDictionary<string, object> messageHeaders, TTransportMessage transportMessage)
        => messageSerializer.Deserialize(messageType, messageHeaders, payloadProvider(transportMessage), transportMessage);
}