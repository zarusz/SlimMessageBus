namespace SlimMessageBus.Host.Outbox.Sql;

public enum SqlOutboxMessageIdGenerationMode
{
    /// <summary>
    /// The database is responsible for generating the message id, using NEWID().
    /// See https://learn.microsoft.com/en-us/sql/t-sql/functions/newid-transact-sql?view=sql-server-ver16
    /// </summary>
    DatabaseGeneratedGuid,
    /// <summary>
    /// The database is responsible for generating the message id, using NEWSEQUENTIALID().
    /// See https://learn.microsoft.com/en-us/sql/t-sql/functions/newsequentialid-transact-sql?view=sql-server-ver16
    /// </summary>
    DatabaseGeneratedSequentialGuid,
    /// <summary>
    /// The client is responsible for generating the message id, using <see cref="IGuidGenerator"/>.
    /// </summary>
    ClientGuidGenerator,
}

