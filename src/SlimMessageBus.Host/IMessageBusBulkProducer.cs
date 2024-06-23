namespace SlimMessageBus.Host;

/// <summary>
/// Interface for bulk publishing of messages directly to a queue.
/// </summary>
/// <remarks>
/// The Interface is intended for outbox publishing where messages have already flowed through the interceptor pipeline.
/// </remarks>
public interface IMessageBusBulkProducer
{
    /// <summary>
    /// The maximum number of messages that can take part in a <see cref="TransactionScope"/>. Null if transaction scopes are not supported by the message bus.
    /// </summary>
    int? MaxMessagesPerTransaction { get; }

    Task<ProduceToTransportBulkResult<T>> ProduceToTransportBulk<T>(IReadOnlyCollection<T> envelopes, string path, IMessageBusTarget targetBus, CancellationToken cancellationToken = default) where T : BulkMessageEnvelope;
}

public record BulkMessageEnvelope(object Message, Type MessageType, IDictionary<string, object> Headers);

public record ProduceToTransportBulkResult<T>(IReadOnlyCollection<T> Dispatched, Exception Exception);