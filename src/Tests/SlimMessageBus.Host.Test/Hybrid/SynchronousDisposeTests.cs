namespace SlimMessageBus.Host.Test.Hybrid;

using System.Threading.Tasks.Sources;

using SlimMessageBus.Host.Collections;
using SlimMessageBus.Host.Hybrid;

public class SynchronousDisposeTests
{
    [Fact]
    public void Dispose_WaitsForPendingValueTaskSource_AndConsumesItOnce()
    {
        using var services = new ServiceCollection()
            .AddSingleton<RuntimeTypeCache>()
            .BuildServiceProvider();
        var source = new PendingDisposeSource();
        var bus = new TestHybridMessageBus(new MessageBusSettings { ServiceProvider = services }, source);

        bus.Dispose();

        source.ContinuationRegistered.Should().BeTrue();
        source.ResultCount.Should().Be(1);
    }

    private sealed class TestHybridMessageBus(MessageBusSettings settings, PendingDisposeSource source)
        : HybridMessageBus(settings, new HybridMessageBusSettings(), MessageBusBuilder.Create())
    {
        protected override ValueTask DisposeAsyncCore() => new(source, 0);
    }

    private sealed class PendingDisposeSource : IValueTaskSource
    {
        private ManualResetValueTaskSourceCore<bool> _source = new() { RunContinuationsAsynchronously = true };

        public bool ContinuationRegistered { get; private set; }
        public int ResultCount { get; private set; }

        public ValueTaskSourceStatus GetStatus(short token) => _source.GetStatus(token);

        public void GetResult(short token)
        {
            _source.GetResult(token);
            ResultCount++;
        }

        public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            ContinuationRegistered = true;
            _source.OnCompleted(continuation, state, token, flags);
            _source.SetResult(true);
        }
    }
}
