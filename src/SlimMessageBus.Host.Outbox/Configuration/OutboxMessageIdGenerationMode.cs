namespace SlimMessageBus.Host.Outbox;

public enum OutboxMessageIdGenerationMode
{
    /// <summary>
    /// The database is responsible for generating the message id, using a guid (NEWID()).
    /// See https://learn.microsoft.com/en-us/sql/t-sql/functions/newid-transact-sql?view=sql-server-ver16
    /// </summary>
    DatabaseGeneratedGuid,
    /// <summary>
    /// The database is responsible for generating the message id, using a sequential guid (NEWSEQUENTIALID()).
    /// See https://learn.microsoft.com/en-us/sql/t-sql/functions/newsequentialid-transact-sql?view=sql-server-ver16
    /// </summary>
    DatabaseGeneratedSequentialGuid,
    /// <summary>
    /// The client is responsible for generating the message id, using <see cref="IGuidGenerator"/>.
    /// </summary>
    ClientGuidGenerator,
}

