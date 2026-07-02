namespace SlimMessageBus.Host.Relational.Benchmark;

public class TestResult
{
    private long _arrivedCount;

    public long ArrivedCount => Interlocked.Read(ref _arrivedCount);

    public void OnArrived() => Interlocked.Increment(ref _arrivedCount);
}
