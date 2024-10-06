namespace SlimMessageBus.Host;

public interface ICurrentTimeProvider
{
    DateTimeOffset CurrentTime { get; }
}
