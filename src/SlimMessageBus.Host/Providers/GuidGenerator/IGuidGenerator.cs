namespace SlimMessageBus.Host;

public interface IGuidGenerator
{
    /// <summary>
    /// Generate a new Guid
    /// </summary>
    /// <returns></returns>
    Guid NewGuid();
}
