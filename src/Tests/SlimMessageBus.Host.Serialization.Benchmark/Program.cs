using BenchmarkDotNet.Running;

namespace SlimMessageBus.Host.Serialization.Benchmark
{
    class Program
    {
        static void Main(string[] args) => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
