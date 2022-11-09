namespace SlimMessageBus.Host.Memory.Benchmark;

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

class Program
{
    static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        /*
        var config = ManualConfig.Create(DefaultConfig.Instance)
              .With(ConfigOptions.JoinSummary)
              .With(ConfigOptions.DisableLogFile);

        BenchmarkRunner.Run(new[]{
            BenchmarkConverter.TypeToBenchmarks( typeof(PubSubBenchmark), config),
            BenchmarkConverter.TypeToBenchmarks( typeof(PubSubWithProducerInterceptorBenchmark), config),
            BenchmarkConverter.TypeToBenchmarks( typeof(PubSubWithPublishInterceptorBenchmark), config),
            BenchmarkConverter.TypeToBenchmarks( typeof(PubSubWithConsumerInterceptorBenchmark), config)
            });
        */
    }
}
