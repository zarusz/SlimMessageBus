namespace SlimMessageBus.Host.Test.Common;

public class CurrentTimeProviderFake : ICurrentTimeProvider
{
    public DateTimeOffset CurrentTime { get; set; } = DateTimeOffset.Now;
}