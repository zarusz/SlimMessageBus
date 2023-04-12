namespace SlimMessageBus.Host;

public interface IMessageHandler
{
    Task<(object Response, Exception ResponseException, string RequestId)> DoHandle(object message, IReadOnlyDictionary<string, object> messageHeaders, IMessageTypeConsumerInvokerSettings consumerInvoker, CancellationToken cancellationToken, object nativeMessage = null, IServiceProvider currentServiceProvider = null);
    Task<object> ExecuteConsumer(object message, IConsumerContext consumerContext, IMessageTypeConsumerInvokerSettings consumerInvoker, Type responseType);
}
