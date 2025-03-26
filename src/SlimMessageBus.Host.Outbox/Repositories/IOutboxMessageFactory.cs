namespace SlimMessageBus.Host.Outbox;

/// <summary>
/// Factory for creating outbox messages
/// </summary>
public interface IOutboxMessageFactory
{
    /// <summary>
    /// Create a new outbox message and store it using the underlying store
    /// </summary>
    /// <param name="busName"></param>
    /// <param name="headers"></param>
    /// <param name="path"></param>
    /// <param name="messageType"></param>
    /// <param name="messagePayload"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>ID of the outbox message.</returns>
    Task<OutboxMessage> Create(
        string busName,
        IDictionary<string, object> headers,
        string path,
        string messageType,
        byte[] messagePayload,
        CancellationToken cancellationToken);
}
