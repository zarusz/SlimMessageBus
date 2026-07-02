namespace SlimMessageBus.Host.PostgreSql;

public class PostgreSqlMessageIdGenerationSettings
{
    public PostgreSqlMessageIdGenerationMode Mode { get; set; } = PostgreSqlMessageIdGenerationMode.ClientGuidGenerator;
    public Type GuidGeneratorType { get; set; } = typeof(PostgreSqlSequentialGuidGenerator);
    public IGuidGenerator? GuidGenerator { get; set; }
}
