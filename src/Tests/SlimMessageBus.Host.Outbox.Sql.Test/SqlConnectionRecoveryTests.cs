namespace SlimMessageBus.Host.Outbox.Sql.Test;

using System.Data;

[Collection(nameof(SqlServerCollection))]
[Trait("Category", "Integration")]
[Trait("Transport", "Outbox.Sql")]
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
        // SqlClient can first receive a server error for a killed session while still
        // reporting Open (notably with managed networking on Linux). Continue until
        // it observes the terminal disconnect rather than assuming the first error
        // has already closed the connection. Bound detection so a regression fails.
        using var disconnectTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (connection.State == ConnectionState.Open)
        {
            disconnectTimeout.Token.ThrowIfCancellationRequested();
            try
            {
                await command.ExecuteScalarAsync(disconnectTimeout.Token);
            }
            catch (SqlException)
            {
                // A server error can precede the transport disconnect.
            }
            catch (InvalidOperationException) when (connection.State != ConnectionState.Open)
            {
                // The driver can close the connection between the state check and execution.
            }
        }
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
