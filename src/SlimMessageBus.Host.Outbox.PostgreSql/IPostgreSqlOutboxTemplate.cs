namespace SlimMessageBus.Host.Outbox.PostgreSql;

public interface IPostgreSqlOutboxTemplate
{
    string AbortDelivery { get; }
    string AllMessages { get; }
    string DeleteSent { get; }
    string IncrementDeliveryAttempt { get; }
    string Insert { get; }
    string LockAndSelect { get; }
    string LockTableAndSelect { get; }
    string MigrationsTableNameQualified { get; }
    string RenewLock { get; }
    string TableNameQualified { get; }
    string UpdateSent { get; }
}