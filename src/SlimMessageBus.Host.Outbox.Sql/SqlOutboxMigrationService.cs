﻿namespace SlimMessageBus.Host.Outbox.Sql;

public class SqlOutboxMigrationService : CommonSqlMigrationService<CommonSqlRepository, SqlSettings>, IOutboxMigrationService
{
    public SqlOutboxMigrationService(ILogger<SqlOutboxMigrationService> logger, ISqlOutboxRepository repository, ISqlTransactionService transactionService, SqlOutboxSettings settings)
        : base(logger, (CommonSqlRepository)repository, transactionService, settings.SqlSettings)
    {
    }

    protected override async Task OnMigrate(CancellationToken token)
    {
        var qualifiedTableName = Repository.GetQualifiedName(Settings.DatabaseTableName);
        var qualifiedOutboxIdTypeName = Repository.GetQualifiedName(Settings.DatabaseOutboxTypeName);

#pragma warning disable CA1861
        await CreateTable(Settings.DatabaseTableName, [
                "Id uniqueidentifier NOT NULL",
                "Timestamp datetime2(7) NOT NULL",
                "BusName nvarchar(64) NOT NULL",
                "MessageType nvarchar(256) NOT NULL",
                "MessagePayload varbinary(max) NOT NULL",
                "Headers nvarchar(max)",
                "Path nvarchar(128)",
                "InstanceId nvarchar(128) NOT NULL",
                "LockInstanceId nvarchar(128) NOT NULL",
                "LockExpiresOn datetime2(7) NOT NULL",
                "DeliveryAttempt int NOT NULL",
                "DeliveryComplete bit NOT NULL",
                $"CONSTRAINT [PK_{Settings.DatabaseTableName}] PRIMARY KEY CLUSTERED([Id] ASC)"
            ],
            token);

        await CreateIndex("IX_Outbox_InstanceId", Settings.DatabaseTableName, [
                "DeliveryComplete",
                "InstanceId"
            ], token);

        await CreateIndex("IX_Outbox_LockExpiresOn", Settings.DatabaseTableName, [
                "DeliveryComplete",
                "LockExpiresOn"
            ], token);

        await CreateIndex("IX_Outbox_Timestamp_LockInstanceId", Settings.DatabaseTableName, [
                "DeliveryComplete",
                "Timestamp",
                "LockInstanceId",
            ], token);
#pragma warning restore CA1861

        await TryApplyMigration("20230120000000_SMB_Init", null, token);

        await TryApplyMigration("20230128225000_SMB_BusNameOptional",
            @$"ALTER TABLE {qualifiedTableName} ALTER COLUMN BusName nvarchar(64) NULL", token);

        await TryApplyMigration("20240502000000_SMB_DeliveryAborted",
            @$"ALTER TABLE {qualifiedTableName} ADD DeliveryAborted BIT NOT NULL DEFAULT 0", token);

        await TryApplyMigration("20240503000000_SMB_Optimizations",
            $"""
            -- unique identifiers must not be clustered
            ALTER TABLE {qualifiedTableName} DROP CONSTRAINT [PK_{Settings.DatabaseTableName}];
            ALTER TABLE {qualifiedTableName} ADD CONSTRAINT [PK_{Settings.DatabaseTableName}] PRIMARY KEY NONCLUSTERED ([Id]);

            -- drop old indexes
            DROP INDEX IX_Outbox_InstanceId ON {qualifiedTableName};
            DROP INDEX IX_Outbox_LockExpiresOn ON {qualifiedTableName};
            DROP INDEX IX_Outbox_Timestamp_LockInstanceId ON {qualifiedTableName};

            -- SqlOutboxTemplate.SqlOutboxMessageLockAndSelect
            CREATE INDEX IX_Outbox_Timestamp_LockInstanceId_LockExpiresOn_DeliveryComplete_0_DeliveryAborted_0 ON {qualifiedTableName} (Timestamp, LockInstanceId, LockExpiresOn) WHERE (DeliveryComplete = 0 and DeliveryAborted = 0);

            -- SqlOutboxTemplate.SqlOutboxMessageLockTableAndSelect
            CREATE INDEX IX_Outbox_LockExpiresOn_LockInstanceId_DeliveryComplete_0_DeliveryAborted_0 ON {qualifiedTableName} (LockExpiresOn, LockInstanceId) WHERE (DeliveryComplete = 0 and DeliveryAborted = 0);
            
            -- SqlOutboxTemplate.SqlOutboxMessageDeleteSent
            CREATE INDEX IX_Outbox_Timestamp_DeliveryComplete_1_DeliveryAborted_0 ON {qualifiedTableName} (Timestamp) WHERE (DeliveryComplete = 1 and DeliveryAborted = 0);

            -- SqlOutboxTemplate.SqlOutboxMessageUpdateSent
            CREATE TYPE {qualifiedOutboxIdTypeName} AS TABLE (Id uniqueidentifier);
            """,
            token);
    }
}
