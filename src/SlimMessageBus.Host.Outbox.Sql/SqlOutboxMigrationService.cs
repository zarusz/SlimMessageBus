namespace SlimMessageBus.Host.Outbox.Sql;

public class SqlOutboxMigrationService : CommonSqlMigrationService<CommonSqlRepository, CommonSqlSettings>, IOutboxMigrationService
{
    public SqlOutboxMigrationService(ILogger<SqlOutboxMigrationService> logger, ISqlOutboxRepository repository, SqlOutboxSettings settings)
        : base(logger, (CommonSqlRepository)repository, settings.SqlSettings)
    {
    }

    protected override async Task OnMigrate(CancellationToken token)
    {
        var qualifiedTableName = Repository.GetTableName(Settings.DatabaseTableName);

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
    }
}
