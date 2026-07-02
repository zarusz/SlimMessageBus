namespace SlimMessageBus.Host.Sql;

public interface ISqlRepository : IRelationalRepository<SqlTransportMessage>
{
    Task EnsureConnection();
}
