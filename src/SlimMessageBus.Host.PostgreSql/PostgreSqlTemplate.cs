namespace SlimMessageBus.Host.PostgreSql;

public class PostgreSqlTemplate
{
    public string TableNameQualified { get; }
    public string SubscriptionsTableNameQualified { get; }
    public string MigrationsTableNameQualified { get; }
    public string SubscriptionsPrimaryKeyName { get; }
    public string ReadyIndexName { get; }
    public string LockIndexName { get; }
    public string MigrationsPrimaryKeyName { get; }
    public string InsertMigration { get; }
    public string InsertWithClientId { get; }
    public string InsertWithDatabaseRandomUuid { get; }
    public string UpsertSubscription { get; }
    public string GetSubscriptions { get; }
    public string LockAndSelect { get; }
    public string Complete { get; }
    public string Fail { get; }
    public string Notify { get; }

    public PostgreSqlTemplate(PostgreSqlMessageBusSettings settings)
    {
        var schemaName = PostgreSqlHelper.QuoteIdentifier(settings.DatabaseSchemaName, nameof(settings.DatabaseSchemaName));
        var tableName = PostgreSqlHelper.QuoteIdentifier(settings.DatabaseTableName, nameof(settings.DatabaseTableName));
        var subscriptionsTableName = PostgreSqlHelper.QuoteIdentifier($"{settings.DatabaseTableName}_subscriptions", nameof(settings.DatabaseTableName));
        var migrationsTableName = PostgreSqlHelper.QuoteIdentifier(settings.DatabaseMigrationsTableName, nameof(settings.DatabaseMigrationsTableName));

        TableNameQualified = $"{schemaName}.{tableName}";
        SubscriptionsTableNameQualified = $"{schemaName}.{subscriptionsTableName}";
        MigrationsTableNameQualified = $"{schemaName}.{migrationsTableName}";
        SubscriptionsPrimaryKeyName = PostgreSqlHelper.QuoteIdentifier($"pk_{settings.DatabaseTableName}_subscriptions", nameof(settings.DatabaseTableName));
        ReadyIndexName = PostgreSqlHelper.QuoteIdentifier($"ix_{settings.DatabaseTableName}_ready", nameof(settings.DatabaseTableName));
        LockIndexName = PostgreSqlHelper.QuoteIdentifier($"ix_{settings.DatabaseTableName}_lock", nameof(settings.DatabaseTableName));
        MigrationsPrimaryKeyName = PostgreSqlHelper.QuoteIdentifier($"PK_{settings.DatabaseMigrationsTableName}", nameof(settings.DatabaseMigrationsTableName));

        InsertMigration = $"""
            with row_inserted as (
                insert into {MigrationsTableNameQualified} ("MigrationId", "ProductVersion")
                values (:migration_id, :product_version)
                on conflict ("MigrationId") do nothing
                returning true as inserted
            )
            select coalesce(inserted, false)
            from row_inserted;
            """;

        string insertWith(string idValue) => $"""
            insert into {TableNameQualified}
            (id, created_on, visible_on, path, path_kind, subscription_name, message_type, message_payload, headers, lock_instance_id, lock_expires_on, delivery_attempt, delivery_complete, delivery_aborted)
            values ({idValue}, now() at time zone 'UTC', now() at time zone 'UTC', :path, :path_kind, :subscription_name, :message_type, :message_payload, :headers, null, null, 0, false, false)
            returning id;
            """;

        InsertWithClientId = insertWith(":id");
        InsertWithDatabaseRandomUuid = insertWith("gen_random_uuid()");

        UpsertSubscription = $"""
            insert into {SubscriptionsTableNameQualified} (path, subscription_name, created_on, last_seen_on)
            values (:path, :subscription_name, now() at time zone 'UTC', now() at time zone 'UTC')
            on conflict (path, subscription_name)
            do update set last_seen_on = excluded.last_seen_on;
            """;

        GetSubscriptions = $"""
            select subscription_name
            from {SubscriptionsTableNameQualified}
            where path = :path;
            """;

        LockAndSelect = $"""
            with batch as (
                select ctid
                from {TableNameQualified}
                where path = :path
                  and path_kind = :path_kind
                  and subscription_name is not distinct from :subscription_name
                  and delivery_complete = false
                  and delivery_aborted = false
                  and visible_on <= now() at time zone 'UTC'
                  and (lock_instance_id = :instance_id or lock_expires_on is null or lock_expires_on < now() at time zone 'UTC')
                order by created_on asc, sequence_id asc
                limit :batch_size
                for update skip locked
            )
            update {TableNameQualified} t
            set lock_instance_id = :instance_id,
                lock_expires_on = now() at time zone 'UTC' + :lock_duration
            from batch b
            where t.ctid = b.ctid
            returning t.id, t.path, t.path_kind, t.subscription_name, t.message_type, t.message_payload, t.headers;
            """;

        Complete = $"""
            update {TableNameQualified}
            set delivery_complete = true,
                delivery_attempt = delivery_attempt + 1
            where id = any(:ids);
            """;

        Fail = $"""
            update {TableNameQualified}
            set delivery_attempt = delivery_attempt + 1,
                delivery_aborted = delivery_attempt + 1 >= :max_delivery_attempts,
                lock_instance_id = null,
                lock_expires_on = null
            where id = any(:ids);
            """;

        Notify = "select pg_notify(:channel, :payload);";
    }
}
