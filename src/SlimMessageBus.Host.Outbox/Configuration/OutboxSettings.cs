namespace SlimMessageBus.Host.Outbox;

using System.Transactions;

public class OutboxSettings
{
    /// <summary>
    /// Processing sequence of messages must be completed in first in, first out. Alternative allows for batch processing across multiple instances of the application.
    /// </summary>
    public bool MaintainSequence { get; set; } = true;
    /// <summary>
    /// The maximum size of the outbox message batch for every database poll.
    /// </summary>
    public int PollBatchSize { get; set; } = 50;
    /// <summary>
    /// Sleep time of the outbox polling loop if there were no messages to process in previous database poll.
    /// </summary>
    public TimeSpan PollIdleSleep { get; set; } = TimeSpan.FromMinutes(1);
    /// <summary>
    /// The maximum number of delivery attempts before delivery will not be attempted again.
    /// </summary>
    public int MaxDeliveryAttempts { get; set; } = 3;
    /// <summary>
    /// Message lock expiration time. When a batch of messages for a bus instance is acquired, the messages will be locked (reserved) for that amount of time.
    /// </summary>
    public TimeSpan LockExpiration { get; set; } = TimeSpan.FromSeconds(10);
    /// <summary>
    /// How long before <see cref="LockExpiration"/> to request a lock renewal. This should be much shorter than <see cref="LockExpiration"/>.
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

