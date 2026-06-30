namespace SlimMessageBus.Host.Sql;

public class SqlMessageIdGenerationSettings
{
    public SqlMessageIdGenerationMode Mode { get; set; } = SqlMessageIdGenerationMode.ClientGuidGenerator;
    public Type GuidGeneratorType { get; set; } = typeof(SqlSequentialGuidGenerator);
    public IGuidGenerator GuidGenerator { get; set; }
}
