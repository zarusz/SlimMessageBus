namespace SlimMessageBus.Host.PostgreSql;

public enum PostgreSqlMessageIdGenerationMode
{
    ClientGuidGenerator,
    DatabaseRandomUuid,
}
