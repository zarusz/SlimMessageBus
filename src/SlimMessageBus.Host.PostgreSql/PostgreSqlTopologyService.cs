namespace SlimMessageBus.Host.PostgreSql;

using System.Reflection;

public class PostgreSqlTopologyService
{
    private readonly ILogger<PostgreSqlTopologyService> _logger;
    private readonly PostgreSqlMessageBusSettings _settings;
    private readonly PostgreSqlRepository _repository;
    private readonly PostgreSqlTemplate _template;

    public PostgreSqlTopologyService(ILogger<PostgreSqlTopologyService> logger, PostgreSqlMessageBusSettings settings, PostgreSqlRepository repository, PostgreSqlTemplate template)
    {
        _logger = logger;
        _settings = settings;
        _repository = repository;
        _template = template;
    }

    public async Task Migrate(CancellationToken cancellationToken)
    {
        await EnsureMigrationHistoryTable(cancellationToken).ConfigureAwait(false);
        await ApplyMigration("20260630000000_SMB_PostgreSql_Transport_Init", $"""
            create extension if not exists pgcrypto;

            create table {_template.TableNameQualified} (
                sequence_id bigserial not null primary key,
                id uuid not null unique,
                created_on timestamptz not null,
                visible_on timestamptz not null,
                path text not null,
                path_kind smallint not null,
                subscription_name text null,
                message_type text not null,
                message_payload bytea not null,
                headers jsonb null,
                lock_instance_id text null,
                lock_expires_on timestamptz null,
                delivery_attempt int not null,
                delivery_complete boolean not null,
                delivery_aborted boolean not null
            );

            create table {_template.SubscriptionsTableNameQualified} (
                path text not null,
                subscription_name text not null,
                created_on timestamptz not null,
                last_seen_on timestamptz not null,
                constraint {_template.SubscriptionsPrimaryKeyName} primary key (path, subscription_name)
            );

            create index {_template.ReadyIndexName}
            on {_template.TableNameQualified} (path, path_kind, subscription_name, visible_on, created_on, sequence_id)
            where delivery_complete = false and delivery_aborted = false;

            create index {_template.LockIndexName}
            on {_template.TableNameQualified} (lock_instance_id, lock_expires_on)
            where delivery_complete = false and delivery_aborted = false;
            """, cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureMigrationHistoryTable(CancellationToken cancellationToken)
    {
        await _repository.EnsureConnection(cancellationToken).ConfigureAwait(false);
        await _repository.ExecuteNonQuery(_settings.SchemaCreationRetry, $"""
            create table if not exists {_template.MigrationsTableNameQualified}
            (
                "MigrationId" character varying(150) not null,
                "ProductVersion" character varying(32) not null,
                constraint {_template.MigrationsPrimaryKeyName} primary key ("MigrationId")
            );
            """, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task ApplyMigration(string migrationId, string sql, CancellationToken cancellationToken)
    {
        var productVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        await PostgreSqlHelper.RetryIfTransientError(_logger, _settings.SchemaCreationRetry, async () =>
        {
            await _repository.EnsureConnection(cancellationToken).ConfigureAwait(false);
#if NETSTANDARD2_0
            using var transaction = _repository.Connection.BeginTransaction();
#else
            await using var transaction = await _repository.Connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
#endif

            using var cmd = _repository.Connection.CreateCommand();
            cmd.Transaction = transaction;
            // SQL identifiers are validated and quoted by PostgreSqlTemplate; identifiers cannot be parameterized. NOSONAR
            cmd.CommandText = _template.InsertMigration;
            cmd.Parameters.AddWithValue("migration_id", migrationId);
            cmd.Parameters.AddWithValue("product_version", productVersion);

            var inserted = (bool?)await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (inserted == true)
            {
                cmd.Parameters.Clear();
                // Migration SQL is provider-owned and built from validated, quoted identifiers. NOSONAR
                cmd.CommandText = sql;
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }
}
