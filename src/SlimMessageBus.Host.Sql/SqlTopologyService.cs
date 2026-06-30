namespace SlimMessageBus.Host.Sql;

public class SqlTopologyService : CommonSqlMigrationService<SqlRepository, SqlMessageBusSettings>
{
    public SqlTopologyService(ILogger<SqlTopologyService> logger, SqlRepository repository, ISqlTransactionService transactionService, SqlMessageBusSettings settings)
        : base(logger, repository, transactionService, settings)
    {
    }

    protected override async Task OnMigrate(CancellationToken token)
    {
        await CreateTable(Settings.DatabaseTableName, [
            "SequenceId bigint IDENTITY(1,1) NOT NULL",
            $"Id uniqueidentifier NOT NULL CONSTRAINT [DF_{Settings.DatabaseTableName}_Id] DEFAULT NEWSEQUENTIALID()",
            "CreatedOn datetime2(7) NOT NULL",
            "VisibleOn datetime2(7) NOT NULL",
            "Path nvarchar(256) NOT NULL",
            "PathKind tinyint NOT NULL",
            "SubscriptionName nvarchar(256) NULL",
            "MessageType nvarchar(512) NOT NULL",
            "MessagePayload varbinary(max) NOT NULL",
            "Headers nvarchar(max) NULL",
            "LockInstanceId nvarchar(128) NULL",
            "LockExpiresOn datetime2(7) NULL",
            "DeliveryAttempt int NOT NULL",
            "DeliveryComplete bit NOT NULL",
            "DeliveryAborted bit NOT NULL",
            $"CONSTRAINT [PK_{Settings.DatabaseTableName}] PRIMARY KEY CLUSTERED ([SequenceId] ASC)",
            $"CONSTRAINT [UX_{Settings.DatabaseTableName}_Id] UNIQUE NONCLUSTERED ([Id] ASC)"
        ], token);

        await CreateTable($"{Settings.DatabaseTableName}Subscriptions", [
            "Path nvarchar(256) NOT NULL",
            "SubscriptionName nvarchar(256) NOT NULL",
            "CreatedOn datetime2(7) NOT NULL",
            "LastSeenOn datetime2(7) NOT NULL",
            $"CONSTRAINT [PK_{Settings.DatabaseTableName}Subscriptions] PRIMARY KEY CLUSTERED ([Path] ASC, [SubscriptionName] ASC)"
        ], token);

        await TryApplyMigration("20260630000000_SMB_SQL_Transport_Init", $"""
            CREATE INDEX [IX_{Settings.DatabaseTableName}_Ready]
            ON {Repository.GetQualifiedName(Settings.DatabaseTableName)} ([Path], [PathKind], [SubscriptionName], [VisibleOn], [CreatedOn], [SequenceId])
            WHERE [DeliveryComplete] = 0 AND [DeliveryAborted] = 0;

            CREATE INDEX [IX_{Settings.DatabaseTableName}_Lock]
            ON {Repository.GetQualifiedName(Settings.DatabaseTableName)} ([LockInstanceId], [LockExpiresOn])
            WHERE [DeliveryComplete] = 0 AND [DeliveryAborted] = 0;
            """, token);
    }
}
