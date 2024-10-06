namespace SlimMessageBus.Host.Outbox.Sql;

public class SqlOutboxMessageIdGenerationSettings
{
    /// <summary>
    /// The mode to use for generating the <see cref="OutboxMessage.Id">.
    /// </summary>
    public SqlOutboxMessageIdGenerationMode Mode { get; set; } = SqlOutboxMessageIdGenerationMode.ClientGuidGenerator;
    /// <summary>
    /// The type to resolve from MSDI that implementes the <see cref="IGuidGenerator"/>.
    /// Default is <see cref="IGuidGenerator"/>.
    /// Guid generator is used to generate unique identifiers for the outbox messages.
    /// </summary>
    public Type GuidGeneratorType { get; set; } = typeof(IGuidGenerator);
    /// <summary>
    /// The instance of <see cref="IGuidGenerator"/> to use (if specified).
    /// Default is null.
    /// Guid generator is used to generate unique identifiers for the outbox messages.
    /// </summary>
    public IGuidGenerator GuidGenerator { get; set; } = null;
}

