namespace SlimMessageBus.Host.PostgreSql;

public interface IPostgreSqlRepository : IRelationalRepository<PostgreSqlTransportMessage>
{
    NpgsqlConnection Connection { get; }
    Task EnsureConnection(CancellationToken cancellationToken);
}
