namespace SlimMessageBus.Host.Outbox.Sql;

public class SqlOutboxTemplate : ISqlOutboxTemplate
{
    public string TableNameQualified { get; }
    public string MigrationsTableNameQualified { get; }
    public string InsertWithClientId { get; }
    public string InsertWithDatabaseId { get; }
    public string InsertWithDatabaseIdSequential { get; }
    public string DeleteSent { get; }
    public string LockAndSelect { get; }
    public string LockTableAndSelect { get; }
    public string UpdateSent { get; }
    public string IncrementDeliveryAttempt { get; }
    public string AbortDelivery { get; }
    public string RenewLock { get; }

    /// <summary>
    /// Used by tests only.
    /// </summary>
    public string AllMessages { get; }

    public string InIdsSeparator { get; } = "|";

    public SqlOutboxTemplate(SqlOutboxSettings settings)
    {
        TableNameQualified = $"[{settings.SqlSettings.DatabaseSchemaName}].[{settings.SqlSettings.DatabaseTableName}]";
        MigrationsTableNameQualified = $"[{settings.SqlSettings.DatabaseSchemaName}].[{settings.SqlSettings.DatabaseMigrationsTableName}]";

        string insertWith(string idFunc) => $"""
            INSERT INTO {TableNameQualified}
            ([Id], [Timestamp], [BusName], [MessageType], [MessagePayload], [Headers], [Path], [InstanceId], [LockInstanceId], [LockExpiresOn], [DeliveryAttempt], [DeliveryComplete], [DeliveryAborted])
            OUTPUT INSERTED.[Id]
            VALUES ({idFunc}, @Timestamp, @BusName, @MessageType, @MessagePayload, @Headers, @Path, @InstanceId, @LockInstanceId, @LockExpiresOn, @DeliveryAttempt, @DeliveryComplete, @DeliveryAborted)
            """;

        InsertWithClientId = insertWith("@Id");
        InsertWithDatabaseId = insertWith("NEWID()");
        InsertWithDatabaseIdSequential = insertWith("NEWSEQUENTIALID()");

        DeleteSent = $"""
            SET DEADLOCK_PRIORITY LOW;
            WITH CTE AS (SELECT TOP (@BatchSize) Id
                         FROM {TableNameQualified} WITH (ROWLOCK, READPAST)
                         WHERE DeliveryComplete = 1
                           AND Timestamp < @Timestamp
                         ORDER BY Timestamp ASC)
            DELETE FROM {TableNameQualified} 
            WHERE Id IN (SELECT Id FROM CTE);
            """;

        LockAndSelect = $"""
            WITH Batch AS (SELECT TOP (@BatchSize) *
                           FROM {TableNameQualified} WITH (ROWLOCK, UPDLOCK, READPAST)
                           WHERE DeliveryComplete = 0
                             AND (LockInstanceId = @InstanceId
                               OR LockExpiresOn < GETUTCDATE())
                             AND DeliveryAborted = 0
                           ORDER BY Timestamp ASC)
            UPDATE Batch
            SET LockInstanceId = @InstanceId,
                LockExpiresOn = DATEADD(SECOND, @LockDuration, GETUTCDATE())
            OUTPUT INSERTED.Id
                 , INSERTED.BusName
                 , INSERTED.MessageType
                 , INSERTED.MessagePayload
                 , INSERTED.Headers
                 , INSERTED.Path;
            """;

        // Only create lock if there are no active locks from another instance.
        // Lock batch + 1 to give preference to current instance in reacquiring table lock.
        LockTableAndSelect = $"""
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
                                  , BusName
                                  , MessageType
                                  , MessagePayload
                                  , Headers
                                  , Path
            FROM {TableNameQualified}
            WHERE LockInstanceId = @InstanceId
                AND LockExpiresOn > GETUTCDATE()
                AND DeliveryComplete = 0
                AND DeliveryAborted = 0
            ORDER BY Timestamp ASC;
            """;

        // See https://learn.microsoft.com/en-us/sql/t-sql/functions/string-split-transact-sql?view=sql-server-ver16
        // See https://stackoverflow.com/a/47777878/1906057
        var inIdsSql = $"SELECT CAST([value] AS uniqueidentifier) Id from STRING_SPLIT(@Ids, '{InIdsSeparator}')";

        UpdateSent = $"""
            UPDATE T
            SET [DeliveryComplete] = 1,
                [DeliveryAttempt] = DeliveryAttempt + 1
            FROM {TableNameQualified} T
                     INNER JOIN ({inIdsSql}) Ids ON T.Id = Ids.id;
            """;

        IncrementDeliveryAttempt = $"""
            UPDATE T
            SET [DeliveryAttempt] = DeliveryAttempt + 1,
                [DeliveryAborted] = CASE WHEN [DeliveryAttempt] >= @MaxDeliveryAttempts THEN 1 ELSE 0 END
            FROM {TableNameQualified} T
                     INNER JOIN ({inIdsSql}) Ids ON T.Id = Ids.id;
            """;

        AbortDelivery = $"""
            UPDATE T
            SET [DeliveryAttempt] = DeliveryAttempt + 1,
                [DeliveryAborted] = 1
            FROM {TableNameQualified} T
                     INNER JOIN ({inIdsSql}) Ids ON T.Id = Ids.id;
            """;

        RenewLock = $"""
            UPDATE {TableNameQualified}
            SET LockExpiresOn = DATEADD(SECOND, @LockDuration, GETUTCDATE())
            WHERE LockInstanceId = @InstanceId
                AND LockExpiresOn > GETUTCDATE()
                AND DeliveryComplete = 0
                AND DeliveryAborted = 0
            """;

        AllMessages = $"""
            SELECT Id
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
            """;
    }
}
