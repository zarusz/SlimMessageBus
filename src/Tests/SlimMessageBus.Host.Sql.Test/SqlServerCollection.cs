namespace SlimMessageBus.Host.Sql.Test;

[CollectionDefinition(nameof(SqlServerCollection))]
public class SqlServerCollection : ICollectionFixture<SqlServerFixture>;
