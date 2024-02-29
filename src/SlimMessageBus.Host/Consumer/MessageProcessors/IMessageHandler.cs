namespace SlimMessageBus.Host;

public interface IMessageHandler
{
    Task<(object Response, Exception ResponseException, string RequestId)> DoHandle(object message, IReadOnlyDictionary<string, object> messageHeaders, IMessageTypeConsumerInvokerSettings consumerInvoker, object nativeMessage = null, IDictionary<string, object> consumerContextProperties = null, IServiceProvider currentServiceProvider = null, CancellationToken cancellationToken = default);
    Task<object> ExecuteConsumer(object message, IConsumerContext consumerContext, IMessageTypeConsumerInvokerSettings consumerInvoker, Type responseType);
}
