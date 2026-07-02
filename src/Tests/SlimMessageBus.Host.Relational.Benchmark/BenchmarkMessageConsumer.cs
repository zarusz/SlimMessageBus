namespace SlimMessageBus.Host.Relational.Benchmark;

public record BenchmarkMessageConsumer(TestResult TestResult) : IConsumer<BenchmarkMessage>
{
    public Task OnHandle(BenchmarkMessage message, CancellationToken cancellationToken)
    {
        TestResult.OnArrived();
        return Task.CompletedTask;
    }
}
