namespace SlimMessageBus.Host.Outbox.Sql;

public class GuidOutboxMessageAdapter(SqlOutboxTemplate sqlOutboxTemplate) : IOutboxMessageAdapter
{
    public OutboxMessage Create()
        => new GuidOutboxMessage
        {
            Id = Guid.NewGuid()
        };

    public OutboxMessage Create(SqlDataReader reader, int idOrdinal)
        => new GuidOutboxMessage
        {
            Id = reader.GetGuid(idOrdinal)
        };

    public SqlParameter CreateIdSqlParameter(string parameterName, OutboxMessage outboxMessage)
    {
        var guidOutboxMessage = (GuidOutboxMessage)outboxMessage;
        return new SqlParameter(parameterName, SqlDbType.UniqueIdentifier) { Value = guidOutboxMessage.Id };
    }

    public SqlParameter CreateIdsSqlParameter(string parameterName, IEnumerable<OutboxMessage> outboxMessages)
    {
        var guidIds = outboxMessages.Cast<GuidOutboxMessage>().Select(x => x.Id);
        var idsString = string.Join(sqlOutboxTemplate.InIdsSeparator, guidIds);
        return new SqlParameter(parameterName, SqlDbType.NVarChar) { Value = guidIds };
    }
}