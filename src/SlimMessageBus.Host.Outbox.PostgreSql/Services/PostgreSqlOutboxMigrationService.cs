namespace SlimMessageBus.Host.Outbox.PostgreSql.Services;

public class PostgreSqlOutboxMigrationService : IOutboxMigrationService
{
    private readonly ILogger<PostgreSqlOutboxMigrationService> _logger;
    private readonly PostgreSqlOutboxSettings _settings;
    private readonly IPostgreSqlRepository _repository;
    private readonly IPostgreSqlOutboxTemplate _template;

    public PostgreSqlOutboxMigrationService(
        ILogger<PostgreSqlOutboxMigrationService> logger,
        PostgreSqlOutboxSettings settings,
        IPostgreSqlRepository repository,
        IPostgreSqlOutboxTemplate template)
    {
        _logger = logger;
        _settings = settings;
        _repository = repository;
        _template = template;
    }

    public async Task Migrate(CancellationToken token)
    {
        await EnsureMigrationHistoryTable(token);
        await ApplyMigration("SMB.Init", $"""
            create table {_template.TableNameQualified} (
                id uuid not null primary key,
                timestamp timestamptz not null,
                bus_name varchar(64),
                message_type varchar(256),
                message_payload bytea not null,
                headers jsonb,
                path varchar(128),
                lock_instance_id varchar(128),
                lock_expires_on timestamptz not null,
                delivery_attempt int not null,
                delivery_complete boolean not null,
                delivery_aborted boolean default false not null
            );            

            -- DeleteSent, LockAndSelect, LockTableAndSelect
            create index ix_smb_outbox_delivery_complete__timestamp__delivery_aborted__false on {_template.TableNameQualified} (delivery_complete, timestamp) where (delivery_aborted = false);

            -- RenewLock
            create index ix_smb_outbox__lock_instance_id__lock_expires_on__delivery_complete__false__delivery_aborted__false on {_template.TableNameQualified} (lock_instance_id, lock_expires_on) where (delivery_complete = false and delivery_aborted = false);

            -- lock table and select
            create or replace function "{_settings.PostgreSqlSettings.DatabaseSchemaName}".smb_outbox_lock_table_and_select(pin_instance_id text, pin_lock_duration interval, pin_batch_size integer)
                returns table
                        (
                            id uuid,
                            bus_name varchar(64),
                            message_type varchar(256),
                            message_payload bytea,
                            headers jsonb,
                            path varchar(128)
                        )
            as
            $$
            begin
                if not exists (select 1
                               from {_template.TableNameQualified} o
                               where o.lock_instance_id <> pin_instance_id
                                 and o.lock_expires_on > now() at time zone 'UTC'
                                 and o.delivery_complete = false
                                 and o.delivery_aborted = false) then
                    with updated_rows as (select o.ctid
                                          from {_template.TableNameQualified} o
                                          where o.delivery_complete = false
                                            and (o.lock_instance_id = pin_instance_id
                                              or o.lock_expires_on < now() at time zone 'UTC')
                                            and o.delivery_aborted = false
                                          order by o.timestamp asc
                                          limit pin_batch_size + 1)
                    update {_template.TableNameQualified} t
                    set lock_instance_id = pin_instance_id
                      , lock_expires_on = now() + pin_lock_duration
                    from updated_rows u
                    where t.ctid = u.ctid;
                end if;

                return query
                    select o.id, o.bus_name, o.message_type, o.message_payload, o.headers, o.path
                    from {_template.TableNameQualified} o
                    where o.lock_instance_id = pin_instance_id
                      and o.lock_expires_on > now() at time zone 'UTC'
                      and o.delivery_complete = false
                      and o.delivery_aborted = false
                    order by o.timestamp asc
                    limit pin_batch_size;
            end;
            $$ language plpgsql;
            """,
            token);
    }

    private async Task EnsureMigrationHistoryTable(CancellationToken cancellationToken)
    {
        var createTableQuery = $"""
            create table if not exists {_template.MigrationsTableNameQualified} 
            (
                "MigrationId" character varying(150) not null,
                "ProductVersion" character varying(32) not null,
                constraint "PK___EFMigrationsHistory" primary key ("MigrationId")
            )
            """;

        await _repository.EnsureConnection(cancellationToken);
        await _repository.ExecuteNonQuery(_settings.PostgreSqlSettings.SchemaCreationRetry, createTableQuery, cancellationToken: cancellationToken);
    }

    [SuppressMessage("SonarLint", "S2077", Justification = "SQL injection - table name is controlled by implementer")]
    private async Task ApplyMigration(string migrationId, string sql, CancellationToken cancellationToken)
    {
        var query = $"""
            with row_inserted AS (
                insert into {_template.MigrationsTableNameQualified} ("MigrationId", "ProductVersion")
                values (@migrationId, @productVersion)
                on conflict ("MigrationId") DO NOTHING
                returning true AS inserted
            )
            select coalesce(inserted, false)
            from row_inserted;
            """;

        var productVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        await PostgreSqlHelper.RetryIfTransientError(_logger, _settings.PostgreSqlSettings.SchemaCreationRetry, async () =>
        {
            var conn = _repository.Connection;
            await _repository.EnsureConnection(cancellationToken);
            NpgsqlTransaction? transaction = null;
            try
            {
#if !NETSTANDARD2_0
                transaction = await conn.BeginTransactionAsync(cancellationToken);
#else
                transaction = conn.BeginTransaction();
#endif

                var cmd = conn.CreateCommand();
                cmd.CommandText = query;
                cmd.Parameters.AddWithValue("migrationId", migrationId);
                cmd.Parameters.AddWithValue("productVersion", productVersion);
                var result = (bool?)(await cmd.ExecuteScalarAsync(cancellationToken));

                if (result == true)
                {
                    cmd = conn.CreateCommand();
                    cmd.CommandText = sql;
                    await cmd.ExecuteNonQueryAsync(cancellationToken);

                    await transaction.CommitAsync(cancellationToken);
                }
            }
            finally
            {
                if (transaction != null)
                {
                    await transaction.DisposeAsync();
                }

                await conn.CloseAsync();
            }
        }, cancellationToken);
    }
}
