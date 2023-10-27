namespace SlimMessageBus.Host;

public interface IResponseProducer
{
    Task ProduceResponse(string requestId, object request, IReadOnlyDictionary<string, object> requestHeaders, object response, Exception responseException, IMessageTypeConsumerInvokerSettings consumerInvoker);
}
