namespace SlimMessageBus.Host.Outbox.Sql;

/// <summary>
/// Allows to customize the outbox message creation and handling (PK type, etc).
/// </summary>
public interface IOutboxMessageAdapter
{
    OutboxMessage Create();
    OutboxMessage Create(SqlDataReader reader, int idOrdinal);
    SqlParameter CreateIdSqlParameter(string parameterName, OutboxMessage outboxMessage);
    SqlParameter CreateIdsSqlParameter(string parameterName, IEnumerable<OutboxMessage> outboxMessages);
}
