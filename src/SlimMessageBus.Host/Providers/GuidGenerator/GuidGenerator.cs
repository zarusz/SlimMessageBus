namespace SlimMessageBus.Host;

public class GuidGenerator : IGuidGenerator
{
    /// <summary>
    /// Generate a new Guid (UUID v4) using <see cref="Guid.NewGuid"/>
    /// </summary>
    public Guid NewGuid() => Guid.NewGuid();
}
