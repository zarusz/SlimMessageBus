namespace SlimMessageBus.Host.Memory.Benchmark;
using System.Threading;

public class TestResult
{
    private long arrivedCount = 0;

    public long ArrivedCount => Interlocked.Read(ref arrivedCount);

    public void OnArrived() => Interlocked.Increment(ref arrivedCount);
}
