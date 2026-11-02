namespace SlimMessageBus.Host.Outbox.Sql.Test;

[Collection(nameof(SqlServerCollection))]
[Trait("Category", "Integration")]
[Trait("Transport", "Outbox.Sql")]
public class CommonSqlMigrationLockTests(SqlServerFixture fixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Migration_AcceptsLockGrantedImmediatelyOrAfterWaiting(bool waitForLock)
    {
        var settings = await CreateSettings();
        await using var connection = new SqlConnection(fixture.GetConnectionString());
        await connection.OpenAsync();
        await using var transactions = new SqlTransactionService(connection, settings);
        var migration = new TestMigration(new TestRepository(connection, transactions, settings), transactions, settings);

        if (waitForLock)
        {
            await using var holder = new SqlConnection(fixture.GetConnectionString());
            await holder.OpenAsync();
            await using var heldTransaction = (SqlTransaction)await holder.BeginTransactionAsync();
            await AcquireMigrationLock(holder, heldTransaction, settings);
            using var sessionCommand = connection.CreateCommand();
            sessionCommand.CommandText = "SELECT @@SPID";
            var sessionId = Convert.ToInt32(await sessionCommand.ExecuteScalarAsync());

            var pendingMigration = migration.Migrate(default);
            try
            {
                // Observe the actual server-side wait instead of relying on timing.
                // Releasing only after this guarantees sp_getapplock returns 1.
                await WaitUntilBlocked(holder, heldTransaction, sessionId, pendingMigration);
            }
            finally
            {
                await heldTransaction.CommitAsync();
                await pendingMigration.WaitAsync(TimeSpan.FromSeconds(15));
            }
        }
        else
        {
            await migration.Migrate(default);
        }

        migration.Applied.Should().BeTrue();
        transactions.CurrentTransaction.Should().BeNull();
    }

    [Fact]
    public async Task Migration_LockTimeoutRollsBack_AndCanRecoverAfterRelease()
    {
        var settings = await CreateSettings();
        await using var holder = new SqlConnection(fixture.GetConnectionString());
        await holder.OpenAsync();
        await using var heldTransaction = (SqlTransaction)await holder.BeginTransactionAsync();
        await AcquireMigrationLock(holder, heldTransaction, settings);
        await using var connection = new SqlConnection(fixture.GetConnectionString());
        await using var transactions = new SqlTransactionService(connection, settings);
        var migration = new TestMigration(new TestRepository(connection, transactions, settings), transactions, settings);

        await Xunit.Assert.ThrowsAsync<TimeoutException>(() => migration.Migrate(default).WaitAsync(TimeSpan.FromSeconds(20)));

        migration.Applied.Should().BeFalse();
        transactions.CurrentTransaction.Should().BeNull();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT OBJECT_ID(@TableName)";
        command.Parameters.AddWithValue("@TableName", $"[{settings.DatabaseSchemaName}].[{settings.DatabaseMigrationsTableName}]");
        (await command.ExecuteScalarAsync()).Should().Be(DBNull.Value);

        await heldTransaction.CommitAsync();
        await migration.Migrate(default);
        migration.Applied.Should().BeTrue();
    }

    private async Task<SqlSettings> CreateSettings()
    {
        var settings = new SqlSettings { DatabaseSchemaName = $"smb_{Guid.NewGuid():N}" };
        settings.SchemaCreationRetry.RetryCount = 1;
        await fixture.CreateSchema(settings.DatabaseSchemaName);
        return settings;
    }

    private static async Task AcquireMigrationLock(SqlConnection connection, SqlTransaction transaction, SqlSettings settings)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sp_getapplock @Resource = @LockName, @LockMode = 'Update',
                @LockOwner = 'Transaction', @LockTimeout = 0, @DbPrincipal = 'public';
            SELECT @result;
            """;
        command.Parameters.AddWithValue("@LockName", $"SMB_Migration.{settings.DatabaseSchemaName}");
        ((int)await command.ExecuteScalarAsync()).Should().Be(0);
    }

    private static async Task WaitUntilBlocked(SqlConnection holder, SqlTransaction transaction, int sessionId, Task migration)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var command = holder.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM sys.dm_exec_requests WHERE session_id = @SessionId AND blocking_session_id = @@SPID";
        command.Parameters.AddWithValue("@SessionId", sessionId);
        while ((int)await command.ExecuteScalarAsync(timeout.Token) == 0)
        {
            migration.IsCompleted.Should().BeFalse("the migration must wait for the held lock");
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed class TestRepository(SqlConnection connection, ISqlTransactionService transactions, SqlSettings settings)
        : CommonSqlRepository(NullLogger.Instance, settings, connection, transactions);

    private sealed class TestMigration(TestRepository repository, ISqlTransactionService transactions, SqlSettings settings)
        : CommonSqlMigrationService<TestRepository, SqlSettings>(NullLogger.Instance, repository, transactions, settings)
    {
        public bool Applied { get; private set; }
        protected override Task OnMigrate(CancellationToken token)
        {
            Applied = true;
            return Task.CompletedTask;
        }
    }
}
