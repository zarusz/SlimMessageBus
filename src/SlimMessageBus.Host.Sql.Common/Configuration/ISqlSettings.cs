namespace SlimMessageBus.Host.Sql.Common;

using System.Data;

public interface ISqlSettings
{
    string DatabaseSchemaName { get; }
    string DatabaseMigrationsTableName { get; }
    string DatabaseTableName { get; }
    SqlDialect Dialect { get; }
    /// <summary>
    /// Initializes the connection <see cref="Microsoft.Data.SqlClient.SqlCommand.CommandTimeout"/> when set to a value.
    /// </summary>
    public TimeSpan? CommandTimeout { get; }

    SqlRetrySettings SchemaCreationRetry { get; }

    SqlRetrySettings OperationRetry { get; }

    /// <summary>
    /// Desired <see cref="TransactionIsolationLevel"/> of the transaction scope created by the consumers (when <see cref="BuilderExtensions.UseTransactionScope(MessageBusBuilder, bool)"/> is enabled).
    /// </summary>
    IsolationLevel TransactionIsolationLevel { get; }
}
