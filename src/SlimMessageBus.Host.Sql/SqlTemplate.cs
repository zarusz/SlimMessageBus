namespace SlimMessageBus.Host.Sql;

public class SqlTemplate
{
    public string TableNameQualified { get; }
    public string SubscriptionsTableNameQualified { get; }
    public string MigrationsTableNameQualified { get; }
    public string InsertWithClientId { get; }
    public string InsertWithDatabaseGuid { get; }
    public string InsertWithDatabaseSequentialGuid { get; }
    public string UpsertSubscription { get; }
    public string GetSubscriptions { get; }
    public string LockAndSelect { get; }
    public string Complete { get; }
    public string Fail { get; }

    public SqlTemplate(SqlMessageBusSettings settings)
    {
        TableNameQualified = $"[{settings.DatabaseSchemaName}].[{settings.DatabaseTableName}]";
        SubscriptionsTableNameQualified = $"[{settings.DatabaseSchemaName}].[{settings.DatabaseTableName}Subscriptions]";
        MigrationsTableNameQualified = $"[{settings.DatabaseSchemaName}].[{settings.DatabaseMigrationsTableName}]";

        string insertWith(string idFunc) => $"""
            INSERT INTO {TableNameQualified}
            ([Id], [CreatedOn], [VisibleOn], [Path], [PathKind], [SubscriptionName], [MessageType], [MessagePayload], [Headers], [LockInstanceId], [LockExpiresOn], [DeliveryAttempt], [DeliveryComplete], [DeliveryAborted])
            OUTPUT INSERTED.[Id]
            VALUES ({idFunc}, SYSUTCDATETIME(), SYSUTCDATETIME(), @Path, @PathKind, @SubscriptionName, @MessageType, @MessagePayload, @Headers, NULL, NULL, 0, 0, 0)
            """;

        InsertWithClientId = insertWith("@Id");
        InsertWithDatabaseGuid = insertWith("NEWID()");
        InsertWithDatabaseSequentialGuid = insertWith("DEFAULT");

        UpsertSubscription = $"""
            MERGE {SubscriptionsTableNameQualified} AS Target
            USING (SELECT @Path AS [Path], @SubscriptionName AS [SubscriptionName]) AS Source
               ON Target.[Path] = Source.[Path] AND Target.[SubscriptionName] = Source.[SubscriptionName]
            WHEN MATCHED THEN
                UPDATE SET [LastSeenOn] = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT ([Path], [SubscriptionName], [CreatedOn], [LastSeenOn])
                VALUES (Source.[Path], Source.[SubscriptionName], SYSUTCDATETIME(), SYSUTCDATETIME());
            """;

        GetSubscriptions = $"""
            SELECT [SubscriptionName]
            FROM {SubscriptionsTableNameQualified}
            WHERE [Path] = @Path
            """;

        LockAndSelect = $"""
            WITH Batch AS (
                SELECT TOP (@BatchSize) *
                FROM {TableNameQualified} WITH (ROWLOCK, UPDLOCK, READPAST)
                WHERE [Path] = @Path
                  AND [PathKind] = @PathKind
                  AND ((@SubscriptionName IS NULL AND [SubscriptionName] IS NULL) OR [SubscriptionName] = @SubscriptionName)
                  AND [DeliveryComplete] = 0
                  AND [DeliveryAborted] = 0
                  AND [VisibleOn] <= SYSUTCDATETIME()
                  AND ([LockInstanceId] = @InstanceId OR [LockExpiresOn] IS NULL OR [LockExpiresOn] < SYSUTCDATETIME())
                ORDER BY [CreatedOn] ASC, [SequenceId] ASC
            )
            UPDATE Batch
            SET [LockInstanceId] = @InstanceId,
                [LockExpiresOn] = DATEADD(SECOND, @LockDuration, SYSUTCDATETIME())
            OUTPUT INSERTED.[Id],
                   INSERTED.[Path],
                   INSERTED.[PathKind],
                   INSERTED.[SubscriptionName],
                   INSERTED.[MessageType],
                   INSERTED.[MessagePayload],
                   INSERTED.[Headers];
            """;

        var idsSql = "SELECT CAST([value] AS uniqueidentifier) Id FROM STRING_SPLIT(@Ids, '|')";

        Complete = $"""
            UPDATE T
            SET [DeliveryComplete] = 1,
                [DeliveryAttempt] = [DeliveryAttempt] + 1
            FROM {TableNameQualified} T
            INNER JOIN ({idsSql}) Ids ON T.[Id] = Ids.[Id];
            """;

        Fail = $"""
            UPDATE T
            SET [DeliveryAttempt] = [DeliveryAttempt] + 1,
                [DeliveryAborted] = CASE WHEN [DeliveryAttempt] + 1 >= @MaxDeliveryAttempts THEN 1 ELSE 0 END,
                [LockInstanceId] = NULL,
                [LockExpiresOn] = NULL
            FROM {TableNameQualified} T
            INNER JOIN ({idsSql}) Ids ON T.[Id] = Ids.[Id];
            """;
    }
}
