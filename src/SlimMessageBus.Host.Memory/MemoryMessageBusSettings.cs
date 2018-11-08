namespace SlimMessageBus.Host.Memory
{
    public class MemoryMessageBusSettings
    {
        /// <summary>
        /// The default behavior is to enable message serialization upon publication and deserialize upon consumption.
        /// This creates an independent message instance in the consumer.
        /// While this is usually a best practice, for performance reasons it might be desired to pass the same message instance and to avoid serialization-deserialization (and effectively avoid creating a deep copy).
        /// Use carefully.
        /// </summary>
        public bool EnableMessageSerialization { get; set; } = false;
    }
}