namespace SlimMessageBus.Host.Test.Common.IntegrationTest;

public class TestMetric
{
    private long _createdConsumerCount;
    private long _processingCount;
    private long _processingCountMax;
    private readonly object _lock = new();

    public long CreatedConsumerCount => Interlocked.Read(ref _createdConsumerCount);

    public long ProcessingCountMax => Interlocked.Read(ref _processingCountMax);

    public void OnCreatedConsumer() => Interlocked.Increment(ref _createdConsumerCount);

    public void OnProcessingStart()
    {
        Interlocked.Increment(ref _processingCount);
        lock (_lock)
        {
            if (_processingCount > _processingCountMax)
            {
                _processingCountMax = _processingCount;
            }
        }
    }

    public void OnProcessingFinish()
    {
        lock (_lock)
        {
            if (_processingCount > _processingCountMax)
            {
                _processingCountMax = _processingCount;
            }
        }
        Interlocked.Decrement(ref _processingCount);
    }
}
