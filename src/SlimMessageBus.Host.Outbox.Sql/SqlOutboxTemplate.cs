namespace SlimMessageBus.Host.Outbox.Sql;

public class SqlOutboxTemplate
{
    public string OutboxIdTypeQualified { get; }
    public string TableNameQualified { get; }
    public string MigrationsTableNameQualified { get; }
    public string SqlOutboxMessageInsert { get; }
    public string SqlOutboxMessageDeleteSent { get; }
    public string SqlOutboxMessageLockAndSelect { get; }
    public string SqlOutboxMessageLockTableAndSelect { get; }
    public string SqlOutboxMessageUpdateSent { get; }
    public string SqlOutboxMessageIncrementDeliveryAttempt { get; }
    public string SqlOutboxMessageRenewLock { get; }

    public SqlOutboxTemplate(SqlOutboxSettings settings)
    {
        OutboxIdTypeQualified = $"[{settings.SqlSettings.DatabaseSchemaName}].[{settings.SqlSettings.DatabaseOutboxTypeName}]";
        TableNameQualified = $"[{settings.SqlSettings.DatabaseSchemaName}].[{settings.SqlSettings.DatabaseTableName}]";
        MigrationsTableNameQualified = $"[{settings.SqlSettings.DatabaseSchemaName}].[{settings.SqlSettings.DatabaseMigrationsTableName}]";

        SqlOutboxMessageInsert = $"""
            INSERT INTO {TableNameQualified}
            ([Id], [Timestamp], [BusName], [MessageType], [MessagePayload], [Headers], [Path], [InstanceId], [LockInstanceId], [LockExpiresOn], [DeliveryAttempt], [DeliveryComplete], [DeliveryAborted])
            VALUES (@Id, @Timestamp, @BusName, @MessageType, @MessagePayload, @Headers, @Path, @InstanceId, @LockInstanceId, @LockExpiresOn, @DeliveryAttempt, @DeliveryComplete, @DeliveryAborted)
            """;

        SqlOutboxMessageDeleteSent = $"""
            DELETE FROM {TableNameQualified} 
            WHERE [DeliveryComplete] = 1 
              AND [Timestamp] < @Timestamp
            """;

        SqlOutboxMessageLockAndSelect = $"""
            WITH Batch AS (SELECT TOP (@BatchSize) *
                           FROM {TableNameQualified}
                           WHERE DeliveryComplete = 0
                             AND (LockInstanceId = @InstanceId
                               OR LockExpiresOn < GETUTCDATE())
                             AND DeliveryAborted = 0
                           ORDER BY Timestamp ASC)
            UPDATE Batch
            SET LockInstanceId = @InstanceId,
                LockExpiresOn = DATEADD(SECOND, @LockDuration, GETUTCDATE())
            OUTPUT INSERTED.Id
                 , INSERTED.Timestamp
                 , INSERTED.BusName
                 , INSERTED.MessageType
                 , INSERTED.MessagePayload
                 , INSERTED.Headers
                 , INSERTED.Path
                 , INSERTED.InstanceId
                 , INSERTED.LockInstanceId
                 , INSERTED.LockExpiresOn
                 , INSERTED.DeliveryAttempt
                 , INSERTED.DeliveryComplete
                 , INSERTED.DeliveryAborted;
            """;

        // Only create lock if there are no active locks from another instance.
        // Lock batch + 1 to give preference to current instance in reacquiring table lock.
        SqlOutboxMessageLockTableAndSelect = $"""
            IF NOT EXISTS (SELECT 1
                           FROM {TableNameQualified}
                           WHERE LockInstanceId <> @InstanceId
                             AND LockExpiresOn > GETUTCDATE()
                             AND DeliveryComplete = 0
                             AND DeliveryAborted = 0)
                BEGIN
                    WITH UpdatedRows AS (SELECT TOP (@BatchSize + 1) LockInstanceId, LockExpiresOn
                                         FROM {TableNameQualified}
                                         WHERE DeliveryComplete = 0
                                           AND (LockInstanceId = @InstanceId
                                             OR LockExpiresOn < GETUTCDATE())
                                           AND DeliveryAborted = 0
                                         ORDER BY Timestamp ASC)
                    UPDATE UpdatedRows
                    SET LockInstanceId = @InstanceId,
                        LockExpiresOn = DATEADD(SECOND, @LockDuration, GETUTCDATE());
                END;

            SELECT TOP (@BatchSize) Id
                                  , Timestamp
                                  , BusName
                                  , MessageType
                                  , MessagePayload
                                  , Headers
                                  , Path
                                  , InstanceId
                                  , LockInstanceId
                                  , LockExpiresOn
                                  , DeliveryAttempt
                                  , DeliveryComplete
                                  , DeliveryAborted
            FROM {TableNameQualified}
            WHERE LockInstanceId = @InstanceId
                AND LockExpiresOn > GETUTCDATE()
                AND DeliveryComplete = 0
                AND DeliveryAborted = 0
            ORDER BY Timestamp ASC;
            """;

        SqlOutboxMessageUpdateSent = $"""
            UPDATE {TableNameQualified}
            SET [DeliveryComplete] = 1,
                [DeliveryAttempt] = DeliveryAttempt + 1
            WHERE [Id] IN (SELECT [Id] from @Ids);
            """;

        SqlOutboxMessageIncrementDeliveryAttempt = $"""
            UPDATE {TableNameQualified}
            SET [DeliveryAttempt] = DeliveryAttempt + 1,
                [DeliveryAborted] = CASE WHEN [DeliveryAttempt] >= @MaxDeliveryAttempts THEN 1 ELSE 0 END
            WHERE [Id] IN (SELECT [Id] from @Ids);
            """;

        SqlOutboxMessageRenewLock = $"""
            UPDATE {TableNameQualified}
            SET LockExpiresOn = DATEADD(SECOND, @LockDuration, GETUTCDATE())
            WHERE LockInstanceId = @InstanceId
                AND LockExpiresOn > GETUTCDATE()
                AND DeliveryComplete = 0
                AND DeliveryAborted = 0
            """;
    }
}
