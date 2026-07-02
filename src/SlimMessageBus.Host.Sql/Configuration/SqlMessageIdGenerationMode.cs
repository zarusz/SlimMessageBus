namespace SlimMessageBus.Host.Sql;

public enum SqlMessageIdGenerationMode
{
    DatabaseGeneratedGuid,
    DatabaseGeneratedSequentialGuid,
    ClientGuidGenerator,
}
