namespace SlimMessageBus.Host.Outbox.Sql;

public interface ISqlMessageOutboxRepository : IOutboxMessageRepository<SqlOutboxMessage>, IOutboxMessageFactory
{
}