﻿namespace SlimMessageBus.Host.Memory.Benchmark;

using BenchmarkDotNet.Running;

class Program
{
    static void Main(string[] args) => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
