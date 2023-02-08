namespace SlimMessageBus.Host.Test.Common.IntegrationTest;

public class TestMetric
{
    private long _createdConsumerCount;

    public long CreatedConsumerCount => Interlocked.Read(ref _createdConsumerCount);

    public void OnCreatedConsumer() => Interlocked.Increment(ref _createdConsumerCount);
}
