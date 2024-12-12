namespace SlimMessageBus.Host;

public delegate void MessageHeaderModifier<in T>(IDictionary<string, object> headers, T message);

public delegate Task ConsumerMethod(object consumer, object message, IConsumerContext consumerContext, CancellationToken cancellationToken);
