namespace SlimMessageBus.Host;

/// <summary>
/// Provides the message payload (binary) from the transport message.
/// </summary>
/// <typeparam name="T"></typeparam>
/// <typeparam name="TPayload"></typeparam>
/// <param name="transportMessage"></param>
/// <returns></returns>
public delegate TPayload MessagePayloadProvider<in T, out TPayload>(T transportMessage);

/// <summary>
/// Initializes the consumer context from the specified transport message.
/// </summary>
/// <typeparam name="T"></typeparam>
/// <param name="transportMessage"></param>
/// <param name="consumerContext"></param>
public delegate void ConsumerContextInitializer<T>(T transportMessage, ConsumerContext consumerContext);

/// <summary>
/// Provides the type of the message from the provided transport message.
/// </summary>
/// <typeparam name="T"></typeparam>
/// <param name="transportMessage"></param>
/// <param name="messageHeaders"></param>
/// <returns></returns>
public delegate Type MessageTypeProvider<in T>(T transportMessage, IReadOnlyDictionary<string, object> messageHeaders);

/// <summary>
/// For a given message type and transport message return the application message.
/// </summary>
/// <typeparam name="T">Type of the transport message</typeparam>
/// <param name="messageType"></param>
/// <param name="messageHeaders"></param>
/// <param name="transportMessage"></param>
/// <returns></returns>
public delegate object MessageProvider<in T>(Type messageType, IReadOnlyDictionary<string, object> messageHeaders, T transportMessage);
