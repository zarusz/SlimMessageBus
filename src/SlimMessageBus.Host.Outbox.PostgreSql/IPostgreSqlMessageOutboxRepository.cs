namespace SlimMessageBus.Host.Outbox.PostgreSql;

public interface IPostgreSqlMessageOutboxRepository : IOutboxMessageRepository<PostgreSqlOutboxMessage>, IOutboxMessageFactory, IPostgreSqlRepository
{
}