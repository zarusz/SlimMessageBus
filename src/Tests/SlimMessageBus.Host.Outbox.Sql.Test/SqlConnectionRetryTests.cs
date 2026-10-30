namespace SlimMessageBus.Host.Outbox.Sql.Test;

using System.Reflection;

public class SqlConnectionRetryTests
{
    private static SqlRetrySettings RetrySettings => new() { RetryCount = 3, RetryInterval = TimeSpan.Zero, RetryIntervalFactor = 1 };

    [Theory]
    [InlineData(-2)]
    [InlineData(53)]
    [InlineData(64)]
    [InlineData(233)]
    [InlineData(10053)]
    [InlineData(10054)]
    [InlineData(10060)]
    public async Task ConnectionOpen_RetriesNetworkFailures_AndRecovers(int errorNumber)
    {
        var attempts = 0;
        var error = CreateSqlException(errorNumber);
        var result = await SqlHelper.RetryIfTransientConnectionError(NullLogger.Instance, RetrySettings,
            () => ++attempts < 3 ? Task.FromException<int>(error) : Task.FromResult(42), default);

        attempts.Should().Be(3);
        result.Should().Be(42);
    }

    [Fact]
    public async Task ConnectionOpen_ExhaustedRetriesPreserveOriginalError()
    {
        var attempts = 0;
        var error = CreateSqlException(10054);
        var actual = await Xunit.Assert.ThrowsAsync<SqlException>(() =>
            SqlHelper.RetryIfTransientConnectionError<int>(NullLogger.Instance, RetrySettings,
                () => { attempts++; return Task.FromException<int>(error); }, default));

        actual.Should().BeSameAs(error);
        attempts.Should().Be(3);
    }

    [Theory]
    [InlineData(18456)] // Login failure: reconnecting must not hide an authentication problem.
    [InlineData(229)] // Permission denied.
    public async Task ConnectionOpen_DoesNotRetryAuthenticationOrPermissionErrors(int errorNumber)
    {
        var attempts = 0;
        await Xunit.Assert.ThrowsAsync<SqlException>(() =>
            SqlHelper.RetryIfTransientConnectionError<int>(NullLogger.Instance, RetrySettings,
                () => { attempts++; return Task.FromException<int>(CreateSqlException(errorNumber)); }, default));

        attempts.Should().Be(1);
    }

    [Fact]
    public async Task CommandPolicy_DoesNotReplayCommandsForNewConnectionErrors()
    {
        var attempts = 0;
        await Xunit.Assert.ThrowsAsync<SqlException>(() =>
            SqlHelper.RetryIfTransientError<int>(NullLogger.Instance, RetrySettings,
                () => { attempts++; return Task.FromException<int>(CreateSqlException(10054)); }, default));

        attempts.Should().Be(1);
    }

    [Fact]
    public async Task ConnectionOpen_CancellationBeforeAttemptDoesNotRunOperation()
    {
        var attempts = 0;
        await Xunit.Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            SqlHelper.RetryIfTransientConnectionError(NullLogger.Instance, RetrySettings,
                () => Task.FromResult(++attempts), new CancellationToken(true)));

        attempts.Should().Be(0);
    }

    [Fact]
    public async Task ConnectionOpen_CancellationAfterFailureDoesNotThrowStaleSqlError()
    {
        using var cancellation = new CancellationTokenSource();
        var attempts = 0;
        await Xunit.Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            SqlHelper.RetryIfTransientConnectionError<int>(NullLogger.Instance, RetrySettings, () =>
            {
                attempts++;
                cancellation.Cancel();
                return Task.FromException<int>(CreateSqlException(10054));
            }, cancellation.Token));

        attempts.Should().Be(1);
    }

    [Fact]
    public async Task ConnectionOpen_CancellationInterruptsRetryBackoff()
    {
        using var cancellation = new CancellationTokenSource();
        var settings = RetrySettings;
        settings.RetryInterval = TimeSpan.FromHours(1);
        var attempts = 0;
        var retry = SqlHelper.RetryIfTransientConnectionError<int>(NullLogger.Instance, settings,
            () => { attempts++; return Task.FromException<int>(CreateSqlException(10054)); }, cancellation.Token);

        cancellation.Cancel();
        await Xunit.Assert.ThrowsAnyAsync<OperationCanceledException>(() => retry.WaitAsync(TimeSpan.FromSeconds(5)));
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task ConnectionOpen_RejectsZeroAttempts()
    {
        await Xunit.Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            SqlHelper.RetryIfTransientConnectionError(NullLogger.Instance, new SqlRetrySettings(),
                () => Task.FromResult(42), default));
    }

    // SqlClient exposes no public constructors for server errors. Construct an error
    // at its public exception boundary so these tests do not require a database outage.
    private static SqlException CreateSqlException(int number)
    {
        var constructor = typeof(SqlError).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .OrderBy(x => x.GetParameters().Length).First();
        var arguments = constructor.GetParameters().Select(p => p.Name == "infoNumber"
            ? (object)number
            : p.ParameterType == typeof(string) ? "simulated SQL failure"
            : p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null).ToArray();
        var error = (SqlError)constructor.Invoke(arguments);
        var errors = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true);
        typeof(SqlErrorCollection).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(errors, [error]);
        return (SqlException)typeof(SqlException).GetMethod("CreateException", BindingFlags.Static | BindingFlags.NonPublic,
            binder: null, [typeof(SqlErrorCollection), typeof(string)], modifiers: null).Invoke(null, [errors, "test"]);
    }
}
