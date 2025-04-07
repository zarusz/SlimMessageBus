namespace SlimMessageBus.Host.Outbox.PostgreSql;

public interface IPostgreSqlRepository
{
    NpgsqlConnection Connection { get; }
    Task EnsureConnection(CancellationToken cancellationToken);
    Task<int> ExecuteNonQuery(RetrySettings retrySettings, string sql, Action<NpgsqlCommand>? setParameters = null, CancellationToken cancellationToken = default);
    Task<object?> ExecuteScalarAsync(RetrySettings retrySettings, string sql, Action<NpgsqlCommand>? setParameters = null, CancellationToken cancellationToken = default);
}