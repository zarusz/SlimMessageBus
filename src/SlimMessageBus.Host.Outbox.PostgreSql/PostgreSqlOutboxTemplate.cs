namespace SlimMessageBus.Host.Outbox.PostgreSql;

public sealed class PostgreSqlOutboxTemplate : IPostgreSqlOutboxTemplate
{
    public string TableNameQualified { get; }
    public string MigrationsTableNameQualified { get; }
    public string AbortDelivery { get; }
    public string AllMessages { get; }
    public string DeleteSent { get; }
    public string IncrementDeliveryAttempt { get; }
    public string Insert { get; }
    public string LockAndSelect { get; }
    public string LockTableAndSelect { get; }
    public string UpdateSent { get; }
    public string RenewLock { get; }

    public PostgreSqlOutboxTemplate(PostgreSqlSettings settings)
    {
        TableNameQualified = $"\"{settings.DatabaseSchemaName}\".\"{settings.DatabaseTableName}\"";
        MigrationsTableNameQualified = $"\"{settings.DatabaseSchemaName}\".\"{settings.DatabaseMigrationsTableName}\"";

        Insert = $"""
            insert into {TableNameQualified}
            (id, timestamp, bus_name, message_type, message_payload, headers, path, lock_instance_id, lock_expires_on, delivery_attempt, delivery_complete, delivery_aborted)
            values (:id, :timestamp, :bus_name, :message_type, :message_payload, :headers, :path, :lock_instance_id, :lock_expires_on, :delivery_attempt, :delivery_complete, :delivery_aborted)
            returning id;
            """;

        DeleteSent = $"""
            with cte as (select ctid
                         from {TableNameQualified} 
                         where delivery_complete = true
                           and delivery_aborted = false
                           and timestamp < :timestamp
                         order by timestamp asc
                         limit :batch_size for update skip locked)
            delete
            from {TableNameQualified} 
            where ctid in (select ctid from cte);
            """;

        LockAndSelect = $"""
            with batch as (
                select ctid
                from {TableNameQualified}
                where delivery_complete = false
                  and (lock_instance_id = :instance_id
                       or lock_expires_on < now() at time zone 'UTC')
                  and delivery_aborted = false
                order by timestamp asc
                limit :batch_size
                for update skip locked
            )
            update {TableNameQualified} t
            set lock_instance_id = :instance_id,
                lock_expires_on = now() + :lock_duration
            from batch b
            where t.ctid = b.ctid
            returning t.id, t.bus_name, t.message_type, t.message_payload, t.headers, t.path;
            """;

        LockTableAndSelect = $"""
            select id, bus_name, message_type, message_payload, headers, path
            from "{settings.DatabaseSchemaName}".smb_outbox_lock_table_and_select(:instance_id, :lock_duration, :batch_size);
            """;

        UpdateSent = $"""
            update {TableNameQualified}
            set delivery_complete = true,
                delivery_attempt = delivery_attempt + 1
            where id = any(:ids);
            """;

        IncrementDeliveryAttempt = $"""
            update {TableNameQualified}
            set delivery_attempt = delivery_attempt + 1,
                delivery_aborted = delivery_attempt >= :max_delivery_attempts
            where id = any(:ids);
            """;

        AbortDelivery = $"""
            update {TableNameQualified}
            set delivery_attempt = delivery_attempt + 1,
                delivery_aborted = true
            where id = any(:ids);
            """;

        RenewLock = $"""
            update {TableNameQualified}
            set lock_expires_on = now() at time zone 'UTC' + :lock_duration
            where lock_instance_id = :instance_id
                and lock_expires_on > now() at time zone 'UTC'
                and delivery_complete = false
                and delivery_aborted = false;
            """;

        AllMessages = $"""
            select id
                 , timestamp
                 , bus_name
                 , message_type
                 , message_payload
                 , headers
                 , path
                 , lock_instance_id
                 , lock_expires_on
                 , delivery_attempt
                 , delivery_complete
                 , delivery_aborted
            from {TableNameQualified}
            """;
    }
}
