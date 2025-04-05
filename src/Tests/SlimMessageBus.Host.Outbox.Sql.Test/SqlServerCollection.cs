namespace SlimMessageBus.Host.Outbox.Sql.Test;

[CollectionDefinition(nameof(SqlServerCollection))]
public class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
}