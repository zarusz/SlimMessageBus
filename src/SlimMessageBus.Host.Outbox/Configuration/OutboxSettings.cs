namespace SlimMessageBus.Host.Outbox;

using System.Transactions;

public class OutboxSettings
{
    /// <summary>
    /// The maximum size of the outbox message batch for every database poll.
    /// </summary>
    public int PollBatchSize { get; set; } = 50;
    /// <summary>
    /// Sleep time of the outbox polling loop if there were no messages to process in previous database poll.
    /// </summary>
    public TimeSpan PollIdleSleep { get; set; } = TimeSpan.FromSeconds(1);
    /// <summary>
    /// Message lock expiration time. When a batch of messages for a bus instance is aquired, the messages will be locked (reserved) for that amount of time.
    /// </summary>
    public TimeSpan LockExpiration { get; set; } = TimeSpan.FromSeconds(10);
    /// <summary>
    /// Latest time before lock expiration where the outbox message will be dispatched to the message broker. This should be much shorter than <see cref="LockExpiration"/>.
    /// </summary>
    public TimeSpan LockExpirationBuffer { get; set; } = TimeSpan.FromSeconds(3);
    /// <summary>
    /// Default <see cref="IsolationLevel"/> for <see cref="TransactionScope"/> transactions.
    /// </summary>
    public IsolationLevel TransactionScopeIsolationLevel { get; set; } = IsolationLevel.RepeatableRead;
    /// <summary>
    /// Sent message cleanup settings.
    /// </summary>
    public OutboxMessageCleanupSettings MessageCleanup { get; set; } = new();

    /// <summary>
    /// Type resolver which is responsible for converting message type into the Outbox table column MessageType
    /// </summary>
    public IMessageTypeResolver MessageTypeResolver { get; set; } = new AssemblyQualifiedNameMessageTypeResolver();
}
