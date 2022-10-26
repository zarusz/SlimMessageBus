namespace SlimMessageBus.Host.Benchmark;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;

/// <summary>
/// The Task<T>.Result is used anytime a response message has to be taken from the message handler. It is important that getting the Task<T> Result is fast.
/// </summary>
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[MemoryDiagnoser]
public class CallConsumerBenchmark
{
    // public field
    [ParamsSource(nameof(Scenarios))]
    public Scenario scenario;

    public IEnumerable<Scenario> Scenarios
    {
        get
        {
            var onHandleMethodInfo = typeof(SomeMessageConsumer).GetMethod(nameof(SomeMessageConsumer.OnHandle), new[] { typeof(SomeMessage), typeof(string) });

            var message = new SomeMessage();
            var consumer = new SomeMessageConsumer();

            return new[]
            {
                new Scenario("Reflection",
                    message,
                    consumer,
                    (target, message, topic) => (Task)onHandleMethodInfo.Invoke(target, new[]{ message, topic })),

                new Scenario("CompiledExpression",
                    message,
                    consumer,
                    ReflectionUtils.GenerateAsyncMethodCallFunc2(onHandleMethodInfo, typeof(SomeMessageConsumer), typeof(SomeMessage), typeof(string)))
            };
        }
    }

    [Benchmark]
    public void CallConsumerOnHandle()
    {
        _ = scenario.OnHandle(scenario.Consumer, scenario.Message, "some-topic");
    }

    public record Scenario(string Name, SomeMessage Message, SomeMessageConsumer Consumer, Func<object, object, object, Task> OnHandle)
    {
        public override string ToString() => Name;
    }

    public record SomeMessage;

    public class SomeMessageConsumer : IConsumer<SomeMessage>
    {
        public Task OnHandle(SomeMessage message) => Task.CompletedTask;
    }
}