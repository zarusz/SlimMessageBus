namespace SlimMessageBus.Host;

public delegate void MessageHeaderModifier<in T>(IDictionary<string, object> headers, T message);