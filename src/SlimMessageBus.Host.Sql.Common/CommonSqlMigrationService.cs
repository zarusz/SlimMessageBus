namespace SlimMessageBus.Host.Sql.Common;

using System.Diagnostics;
using System.Reflection;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

public abstract class CommonSqlMigrationService<TRepository, TSettings>
    where TRepository : CommonSqlRepository
    where TSettings : ISqlSettings
{
    protected ILogger Logger { get; }
    protected TSettings Settings { get; }
    protected TRepository Repository { get; }
    public ISqlTransactionService TransactionService { get; }

    protected CommonSqlMigrationService(ILogger logger, TRepository repository, ISqlTransactionService transactionService, TSettings settings)
    {
        Logger = logger;
        Settings = settings;
        Repository = repository;
        TransactionService = transactionService;
    }

    protected async Task<bool> TryApplyMigration(string migrationId, string migrationSql, CancellationToken token)
    {
        var versionId = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        var migrationsTableName = Repository.GetQualifiedName(Settings.DatabaseMigrationsTableName);

        Logger.LogTrace("Ensuring migration {MigrationId} is applied", migrationId);
        var affected = await Repository.ExecuteNonQuery(Settings.SchemaCreationRetry,
            @$"IF NOT EXISTS (SELECT * FROM {migrationsTableName} WHERE MigrationId = '{migrationId}')
            BEGIN 
                INSERT INTO {migrationsTableName} (MigrationId, ProductVersion) VALUES ('{migrationId}', '{versionId}')
            END", token: token);

        if (affected > 0)
        {
            if (migrationSql != null)
            {
                Logger.LogDebug("Executing migration {MigrationId}...", migrationId);
                await Repository.ExecuteNonQuery(Settings.SchemaCreationRetry, migrationSql, token: token);
            }
            return true;
        }
        return false;
    }

    protected async Task CreateTable(string tableName, IEnumerable<string> columns, CancellationToken token)
    {
        var qualifiedTableName = Repository.GetQualifiedName(tableName);

        Logger.LogDebug("Ensuring table {TableName} is created", tableName);
        await Repository.ExecuteNonQuery(Settings.SchemaCreationRetry,
            @$"IF OBJECT_ID('{qualifiedTableName}') IS NULL 
            BEGIN 
                CREATE TABLE {qualifiedTableName} 
                (
                    {string.Join(",", columns)}
                )
            END", token: token);
    }

    protected async Task CreateIndex(string indexName, string tableName, IEnumerable<string> columns, CancellationToken token)
    {
        var qualifiedTableName = Repository.GetQualifiedName(tableName);

        Logger.LogDebug("Ensuring index {IndexName} on table {TableName} is created", indexName, qualifiedTableName);

        await Repository.ExecuteNonQuery(Settings.SchemaCreationRetry,
            @$"IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = '{indexName}' AND object_id = OBJECT_ID('{qualifiedTableName}'))
            BEGIN 
                CREATE NONCLUSTERED INDEX [{indexName}] ON {qualifiedTableName}
                (
                    {string.Join(",", columns.Select(c => $"{c} ASC"))}
                )
            END", token: token);
    }

    public async virtual Task Migrate(CancellationToken token)
    {
        await Repository.EnsureConnection();

        try
        {
            Logger.LogInformation("Database schema provisioning started...");

            // Retry few times to create the schema - perhaps there are concurrently running other service process-es that attempt to do the same (distributed micro-service).
            await SqlHelper.RetryIfError(Logger, Settings.SchemaCreationRetry, _ => true, async () =>
            {
                await TransactionService.BeginTransaction();
                try
                {
                    if (!await TryAcquireLock($"SMB_Migration.{Settings.DatabaseSchemaName}", TimeSpan.FromSeconds(10), token))
                    {
                        Logger.LogWarning("Failed to obtain exclusive lock on database migration");
                        throw new TimeoutException("Failed to obtain exclusive lock on database migration");
                    }

                    await CreateTable(Settings.DatabaseMigrationsTableName, new[] {
                            "MigrationId nvarchar(150) NOT NULL",
                            "ProductVersion nvarchar(32) NOT NULL",
                            $"CONSTRAINT [PK_{Settings.DatabaseMigrationsTableName}] PRIMARY KEY CLUSTERED ([MigrationId] ASC)"
                        },
                        token);

                    await OnMigrate(token);

                    await TransactionService.CommitTransaction();
                    return true;
                }
                catch (Exception)
                {
                    await TransactionService.RollbackTransaction();
                    throw;
                }
            }, token);

            Logger.LogInformation("Database schema provisioning finished");
        }
        catch (SqlException e)
        {
            Logger.LogError(e, "Database schema provisioning encountered a non-recoverable SQL error: {ErrorMessage}", e.Message);
            throw;
        }
    }

    protected abstract Task OnMigrate(CancellationToken token);

    private async Task<bool> TryAcquireLock(string lockName, TimeSpan timeout, CancellationToken token = default)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(lockName));

        var result = await Repository.ExecuteNonQuery<int>(Settings.SchemaCreationRetry, "sp_getapplock", cmd =>
        {
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@Resource", lockName);
            cmd.Parameters.AddWithValue("@LockMode", "Update");
            cmd.Parameters.AddWithValue("@LockOwner", "transaction");
            cmd.Parameters.AddWithValue("@LockTimeout", timeout.TotalMilliseconds);
            cmd.Parameters.AddWithValue("@DbPrincipal", "public");
            return cmd.Parameters.Add(new SqlParameter { Direction = ParameterDirection.ReturnValue });
        }, token: token);

        return result == 0;
    }
}