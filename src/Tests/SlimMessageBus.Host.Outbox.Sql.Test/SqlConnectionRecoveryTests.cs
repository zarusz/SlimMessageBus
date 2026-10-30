namespace SlimMessageBus.Host.Outbox.Sql.Test;

using System.Data;

[Collection(nameof(SqlServerCollection))]
public class SqlConnectionRecoveryTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task KilledConnection_CanBeReopenedOnSameRepository()
    {
        var connectionString = new SqlConnectionStringBuilder(fixture.GetConnectionString())
        {
            ConnectRetryCount = 0, // Exercise repository recovery rather than driver idle reconnect.
            Pooling = false
        }.ConnectionString;
        await using var connection = new SqlConnection(connectionString);
        await using var transactions = new SqlTransactionService(connection, new SqlSettings());
        var repository = new TestRepository(connection, transactions);
        await repository.EnsureConnection(default);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT @@SPID";
        var sessionId = Convert.ToInt32(await command.ExecuteScalarAsync());

        await using var admin = new SqlConnection(fixture.GetConnectionString());
        await admin.OpenAsync();
        using var kill = admin.CreateCommand();
        kill.CommandText = $"KILL {sessionId}";
        await kill.ExecuteNonQueryAsync();
        command.CommandText = "SELECT 42";
        await Xunit.Assert.ThrowsAsync<SqlException>(() => command.ExecuteScalarAsync());
        connection.State.Should().NotBe(ConnectionState.Open);

        await repository.EnsureConnection(default);
        ((int)await command.ExecuteScalarAsync()).Should().Be(42);
    }

    [Fact]
    public async Task DisconnectedTransaction_IsNotSilentlyReopened()
    {
        await using var connection = new SqlConnection(fixture.GetConnectionString());
        var transactions = new SqlTransactionService(connection, new SqlSettings());
        var repository = new TestRepository(connection, transactions);
        await repository.EnsureConnection(default);
        await transactions.BeginTransaction();
        await connection.CloseAsync();

        await Xunit.Assert.ThrowsAsync<MessageBusException>(() => repository.EnsureConnection(default));
        connection.State.Should().Be(ConnectionState.Closed);
    }

    [Fact]
    public async Task CanceledOpen_LeavesConnectionClosed()
    {
        await using var connection = new SqlConnection(fixture.GetConnectionString());
        await using var transactions = new SqlTransactionService(connection, new SqlSettings());
        var repository = new TestRepository(connection, transactions);

        await Xunit.Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.EnsureConnection(new CancellationToken(true)));
        connection.State.Should().Be(ConnectionState.Closed);
    }

    private sealed class TestRepository(SqlConnection connection, ISqlTransactionService transactions)
        : CommonSqlRepository(NullLogger.Instance, new SqlSettings(), connection, transactions);
}
