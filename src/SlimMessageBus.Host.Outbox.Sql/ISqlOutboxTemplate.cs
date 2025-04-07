namespace SlimMessageBus.Host.Outbox.Sql;

public interface ISqlOutboxTemplate
{
    string AbortDelivery { get; }
    string AllMessages { get; }
    string DeleteSent { get; }
    string IncrementDeliveryAttempt { get; }
    string InIdsSeparator { get; }
    string InsertWithClientId { get; }
    string InsertWithDatabaseId { get; }
    string InsertWithDatabaseIdSequential { get; }
    string LockAndSelect { get; }
    string LockTableAndSelect { get; }
    string MigrationsTableNameQualified { get; }
    string RenewLock { get; }
    string TableNameQualified { get; }
    string UpdateSent { get; }
}