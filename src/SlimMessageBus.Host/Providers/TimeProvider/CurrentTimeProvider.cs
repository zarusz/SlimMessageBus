namespace SlimMessageBus.Host;

public class CurrentTimeProvider : ICurrentTimeProvider
{
    public DateTimeOffset CurrentTime => DateTimeOffset.UtcNow;
}