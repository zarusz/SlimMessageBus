namespace SlimMessageBus.Host.Sql;

public class SqlTemplate
{
    public string TableNameQualified { get; }
    public string MigrationsTableNameQualified { get; }

    public SqlTemplate(SqlMessageBusSettings settings)
    {
        TableNameQualified = $"[{settings.DatabaseSchemaName}].[{settings.DatabaseTableName}]";
        MigrationsTableNameQualified = $"[{settings.DatabaseSchemaName}].[{settings.DatabaseMigrationsTableName}]";
    }
}
