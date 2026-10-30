namespace SlimMessageBus.Host.Outbox.Sql.Test;

[Collection(nameof(SqlServerCollection))]
[Trait("Category", "Integration")]
[Trait("Transport", "Outbox.Sql")]
public class SqlOutboxMigrationRetryTests(SqlServerFixture fixture)
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task FailedMigration_UsesFreshTransactions_AndCanRecover(int failures)
    {
        var settings = new SqlOutboxSettings();
        settings.SqlSettings.DatabaseSchemaName = $"smb_{Guid.NewGuid():N}";
        settings.SqlSettings.SchemaCreationRetry.RetryCount = 3;
        settings.SqlSettings.SchemaCreationRetry.RetryInterval = TimeSpan.Zero;
        await fixture.CreateSchema(settings.SqlSettings.DatabaseSchemaName);
        await using var connection = new SqlConnection(fixture.GetConnectionString());
        await using var transactions = new RecordingTransactionService(connection, settings.SqlSettings);
        var repository = new SqlOutboxMessageRepository(NullLogger<SqlOutboxMessageRepository>.Instance,
            settings, new SqlOutboxTemplate(settings), new GuidGenerator(), TimeProvider.System,
            new DefaultInstanceIdProvider(), connection, transactions);
        var migration = new FailingMigrationService(repository, transactions, settings, failures);

        if (failures == 3)
        {
            // Exhaust retries and verify the original SQL error survives, then simulate
            // the underlying database issue being fixed before the next migration call.
            var error = await Xunit.Assert.ThrowsAsync<SqlException>(() => migration.Migrate(default));
            error.Number.Should().Be(50000);
            error.Message.Should().Contain("migration permission failure");
        }

        await migration.Migrate(default);

        migration.Attempts.Should().Be(failures + 1);
        transactions.Transactions.Should().HaveCount(failures + 1);
        transactions.Transactions.Distinct().Should().HaveCount(failures + 1);
        transactions.CurrentTransaction.Should().BeNull();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {repository.GetQualifiedName(settings.SqlSettings.DatabaseMigrationsTableName)}";
        ((int)await command.ExecuteScalarAsync()).Should().BeGreaterThan(0);
    }

    private sealed class RecordingTransactionService(SqlConnection connection, SqlSettings settings)
        : SqlTransactionService(connection, settings)
    {
        public List<SqlTransaction> Transactions { get; } = [];

        protected override async Task OnBeginTransaction()
        {
            await base.OnBeginTransaction();
            Transactions.Add(CurrentTransaction);
        }
    }

    private sealed class FailingMigrationService(
        SqlOutboxMessageRepository repository,
        ISqlTransactionService transactions,
        SqlOutboxSettings settings,
        int failures)
        : SqlOutboxMigrationService(NullLogger<SqlOutboxMigrationService>.Instance, repository, transactions, settings)
    {
        public int Attempts { get; private set; }

        protected override async Task OnMigrate(CancellationToken token)
        {
            if (++Attempts <= failures)
            {
                using var command = repository.Connection.CreateCommand();
                command.Transaction = TransactionService.CurrentTransaction;
                command.CommandText = "THROW 50000, 'migration permission failure', 1;";
                await command.ExecuteNonQueryAsync(token);
            }
            await base.OnMigrate(token);
        }
    }
}
