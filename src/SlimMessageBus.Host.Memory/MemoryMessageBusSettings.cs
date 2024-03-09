namespace SlimMessageBus.Host.Memory;

public class MemoryMessageBusSettings
{
    /// <summary>
    /// The default behavior is to disable message serialization after publication and deserialize prior consumption.
    /// When serialization is enabled, it creates an independent message instance in the consumer (deep copy of the original message).
    /// While this is usually a best practice, for performance reasons it might be desired to pass the same message instance and to avoid serialization-deserialization (and effectively avoid creating a deep copy).
    /// Disabling serialization might be useful if your domain events have references to domain objects that you would want to preserve and not create deep copies of.
    /// </summary>
    public bool EnableMessageSerialization { get; set; } = false;

    /// <summary>
    /// The default behavior is to have Publish operations blocking (synchronous) and to wait for the message processing (handling) to finish.
    /// This is useful to ensure side effect within the unit of work (web request, external message handling) are completed.
    /// However, if you prefer to have Publish operaions non-blocking (asynchronous), you can disable this setting.
    /// </summary>
    public bool EnableBlockingPublish { get; set; } = true;
}