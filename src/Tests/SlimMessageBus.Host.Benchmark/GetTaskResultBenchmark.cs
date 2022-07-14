namespace SlimMessageBus.Host.Benchmark
{
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Order;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// The Task<T>.Result is used anytime a response message has to be taken from the message handler. It is important that getting the Task<T> Result is fast.
    /// </summary>
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [MemoryDiagnoser]
    public class GetTaskResultBenchmark
    {
        // public field
        [ParamsSource(nameof(Scenarios))]
        public Scenario scenario;

        public IEnumerable<Scenario> Scenarios
        {
            get
            {
                var taskResultPropertyInfo = typeof(Task<>).MakeGenericType(typeof(int)).GetProperty(nameof(Task<int>.Result));
                var taskResultPropertyInfoGetMethod = taskResultPropertyInfo.GetGetMethod();

                return new[]
                {
                    new Scenario("Reflection",
                        Task.FromResult(1),
                        target => taskResultPropertyInfoGetMethod.Invoke(target, null)),
                    new Scenario("CompiledExpression",
                        Task.FromResult(1),
                        ReflectionUtils.GenerateGetterLambda(taskResultPropertyInfo))
                };
            }
        }

        [Benchmark]
        public void GetTaskResult()
        {
            _ = scenario.GetResult(scenario.Task);
        }

        public record Scenario(string Name, Task Task, Func<object, object> GetResult)
        {
            public override string ToString() => Name;
        }
    }
}