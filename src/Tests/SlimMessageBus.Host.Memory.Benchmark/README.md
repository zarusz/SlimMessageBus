Sample Benchmark results

```
// * Summary *

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.2605)
12th Gen Intel Core i7-1260P, 1 CPU, 16 logical and 12 physical cores
.NET SDK 9.0.100
  [Host]     : .NET 8.0.11 (8.0.1124.51707), X64 RyuJIT AVX2
  Job-SXUBYX : .NET 8.0.11 (8.0.1124.51707), X64 RyuJIT AVX2

MaxIterationCount=30  MaxWarmupIterationCount=10

```

| Type                                   | Method                        | messageCount | createMessageScope | Mean     | Error    | StdDev   | Gen0        | Gen1      | Gen2      | Allocated |
|--------------------------------------- |------------------------------ |------------- |------------------- |---------:|---------:|---------:|------------:|----------:|----------:|----------:|
| PubSubBenchmark                        | PubSub                        | 1000000      | False              | 651.3 ms | 11.20 ms | 11.51 ms | 116000.0000 | 3000.0000 | 3000.0000 |   1.02 GB |
| PubSubWithConsumerInterceptorBenchmark | PubSubWithConsumerInterceptor | 1000000      | False              | 729.8 ms | 14.52 ms | 12.12 ms | 144000.0000 | 3000.0000 | 3000.0000 |   1.26 GB |
| PubSubWithProducerInterceptorBenchmark | PubSubWithProducerInterceptor | 1000000      | False              | 759.4 ms | 12.06 ms | 11.28 ms | 154000.0000 | 3000.0000 | 3000.0000 |   1.35 GB |
| PubSubWithPublishInterceptorBenchmark  | PubSubWithPublishInterceptor  | 1000000      | False              | 752.2 ms | 10.63 ms |  9.94 ms | 154000.0000 | 3000.0000 | 3000.0000 |   1.35 GB |
| PubSubBenchmark                        | PubSub                        | 1000000      | True               | 673.1 ms |  6.32 ms |  5.91 ms | 130000.0000 | 3000.0000 | 3000.0000 |   1.14 GB |
| PubSubWithConsumerInterceptorBenchmark | PubSubWithConsumerInterceptor | 1000000      | True               | 769.8 ms | 10.16 ms |  9.01 ms | 157000.0000 | 3000.0000 | 3000.0000 |   1.38 GB |
| PubSubWithProducerInterceptorBenchmark | PubSubWithProducerInterceptor | 1000000      | True               | 789.1 ms | 14.11 ms | 12.51 ms | 167000.0000 | 3000.0000 | 3000.0000 |   1.47 GB |
| PubSubWithPublishInterceptorBenchmark  | PubSubWithPublishInterceptor  | 1000000      | True               | 802.6 ms |  9.42 ms |  7.87 ms | 167000.0000 | 3000.0000 | 3000.0000 |   1.47 GB |

```
// * Hints *
Outliers
  PubSubBenchmark.PubSub: MaxIterationCount=30, MaxWarmupIterationCount=10
 -> 2 outliers were removed (689.29 ms, 692.70 ms)
  PubSubWithConsumerInterceptorBenchmark.PubSubWithConsumerInterceptor: MaxIterationCount=30, MaxWarmupIterationCount=10 -> 2 outliers were removed (771.75 ms, 783.11 ms)
  PubSubBenchmark.PubSub: MaxIterationCount=30, MaxWarmupIterationCount=10
 -> 1 outlier  was  detected (662.74 ms)
  PubSubWithConsumerInterceptorBenchmark.PubSubWithConsumerInterceptor: MaxIterationCount=30, MaxWarmupIterationCount=10 -> 1 outlier  was  removed (797.48 ms)
  PubSubWithProducerInterceptorBenchmark.PubSubWithProducerInterceptor: MaxIterationCount=30, MaxWarmupIterationCount=10 -> 1 outlier  was  removed (842.72 ms)
  PubSubWithPublishInterceptorBenchmark.PubSubWithPublishInterceptor: MaxIterationCount=30, MaxWarmupIterationCount=10   -> 2 outliers were removed, 3 outliers were detected (786.09 ms, 821.03 ms, 827.84 ms)

// * Legends *
  messageCount       : Value of the 'messageCount' parameter
  createMessageScope : Value of the 'createMessageScope' parameter
  Mean               : Arithmetic mean of all measurements
  Error              : Half of 99.9% confidence interval
  StdDev             : Standard deviation of all measurements
  Gen0               : GC Generation 0 collects per 1000 operations
  Gen1               : GC Generation 1 collects per 1000 operations
  Gen2               : GC Generation 2 collects per 1000 operations
  Allocated          : Allocated memory per single operation (managed only, inclusive, 1KB = 1024B)
  1 ms               : 1 Millisecond (0.001 sec)

// * Diagnostic Output - MemoryDiagnoser *


// ***** BenchmarkRunner: End *****
Global total time: 00:03:23 (203.54 sec), executed benchmarks: 8
// * Artifacts cleanup *
Artifacts cleanup is finished
```
