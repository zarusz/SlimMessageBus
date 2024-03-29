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
            var onHandleMethodInfo = typeof(SomeMessageConsumer).GetMethod(nameof(SomeMessageConsumer.OnHandle), new[] { typeof(SomeMessage) });

            var message = new SomeMessage();
            var consumer = new SomeMessageConsumer();

            return new[]
            {
                new Scenario("Reflection",
                    message,
                    consumer,
                    (target, message) => (Task)onHandleMethodInfo.Invoke(target, new[]{ message })),

                new Scenario("CompiledExpression",
                    message,
                    consumer,
                    ReflectionUtils.GenerateMethodCallToFunc<Func<object, object, Task>>(onHandleMethodInfo, typeof(SomeMessageConsumer), typeof(Task), typeof(SomeMessage))),

                new Scenario("CompiledExpressionWithOptional",
                    message,
                    consumer,
                    ReflectionUtils.GenerateMethodCallToFunc<Func<object, object, Task>>(onHandleMethodInfo, [typeof(SomeMessage)]))
            };
        }
    }

    [Benchmark]
    public void CallConsumerOnHandle()
    {
        _ = scenario.OnHandle(scenario.Consumer, scenario.Message);
    }

    public record Scenario(string Name, SomeMessage Message, SomeMessageConsumer Consumer, Func<object, object, Task> OnHandle)
    {
        public override string ToString() => Name;
    }

    public record SomeMessage;

    public class SomeMessageConsumer : IConsumer<SomeMessage>
    {
        public Task OnHandle(SomeMessage message) => Task.CompletedTask;
    }
}