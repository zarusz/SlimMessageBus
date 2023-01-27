namespace SlimMessageBus.Host.Outbox.Sql;
public class SqlOutboxTemplate
{
    public string TableNameQualified { get; }
    public string MigrationsTableNameQualified { get; }

    public string SqlOutboxMessageInsert { get; }
    public string SqlOutboxMessageFindNextSelect { get; }
    public string SqlOutboxMessageTryLockUpdate { get; }
    public string SqlOutboxMessageDeleteSent { get; }

    public SqlOutboxTemplate(SqlOutboxSettings settings)
    {
        TableNameQualified = $"[{settings.DatabaseSchemaName}].[{settings.DatabaseTableName}]";
        MigrationsTableNameQualified = $"[{settings.DatabaseSchemaName}].[{settings.DatabaseMigrationsTableName}]";

        SqlOutboxMessageInsert = @$"INSERT INTO {TableNameQualified}
                ([Id], [Timestamp], [BusName], [MessageType], [MessagePayload], [Headers], [Path], [InstanceId], [LockInstanceId], [LockExpiresOn], [DeliveryAttempt], [DeliveryComplete])
            VALUES
                (@Id, @Timestamp, @BusName, @MessageType, @MessagePayload, @Headers, @Path, @InstanceId, @LockInstanceId, @LockExpiresOn, @DeliveryAttempt, @DeliveryComplete)";

        SqlOutboxMessageFindNextSelect = @$"SELECT TOP {settings.PollBatchSize} * FROM {TableNameQualified} WHERE DeliveryComplete = 0 AND LockInstanceId = @InstanceId ORDER BY Timestamp ASC";
        SqlOutboxMessageTryLockUpdate = @$"UPDATE {TableNameQualified} SET LockInstanceId = @InstanceId, LockExpiresOn = @ExpiresOn WHERE DeliveryComplete = 0 AND LockExpiresOn < GetUtcDate() OR LockInstanceId = @InstanceId";
        SqlOutboxMessageDeleteSent = @$"DELETE FROM {TableNameQualified} WHERE [DeliveryComplete] = 1 AND [Timestamp] < @Timestamp";
    }
}
