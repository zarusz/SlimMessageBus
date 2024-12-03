Sample Benchmark results

```
// * Summary *

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.2454)
12th Gen Intel Core i7-1260P, 1 CPU, 16 logical and 12 physical cores
.NET SDK 9.0.100
  [Host]     : .NET 8.0.11 (8.0.1124.51707), X64 RyuJIT AVX2
  Job-WFUQPN : .NET 8.0.11 (8.0.1124.51707), X64 RyuJIT AVX2

MaxIterationCount=30  MaxWarmupIterationCount=10
```

| Type                                   | Method                        | messageCount | createMessageScope |     Mean |    Error |   StdDev |        Gen0 |      Gen1 |      Gen2 | Allocated |
| -------------------------------------- | ----------------------------- | ------------ | ------------------ | -------: | -------: | -------: | ----------: | --------: | --------: | --------: |
| PubSubBenchmark                        | PubSub                        | 1000000      | False              | 730.6 ms | 14.58 ms | 17.36 ms | 119000.0000 | 3000.0000 | 3000.0000 |   1.04 GB |
| PubSubWithConsumerInterceptorBenchmark | PubSubWithConsumerInterceptor | 1000000      | False              | 810.5 ms | 16.16 ms | 16.59 ms | 146000.0000 | 3000.0000 | 3000.0000 |   1.28 GB |
| PubSubWithProducerInterceptorBenchmark | PubSubWithProducerInterceptor | 1000000      | False              | 823.5 ms |  7.74 ms |  6.86 ms | 156000.0000 | 3000.0000 | 3000.0000 |   1.37 GB |
| PubSubWithPublishInterceptorBenchmark  | PubSubWithPublishInterceptor  | 1000000      | False              | 831.8 ms |  9.43 ms |  7.87 ms | 156000.0000 | 3000.0000 | 3000.0000 |   1.37 GB |
| PubSubBenchmark                        | PubSub                        | 1000000      | True               | 794.8 ms |  5.44 ms |  4.54 ms | 137000.0000 | 3000.0000 | 3000.0000 |    1.2 GB |
| PubSubWithConsumerInterceptorBenchmark | PubSubWithConsumerInterceptor | 1000000      | True               | 900.9 ms | 11.65 ms | 10.32 ms | 164000.0000 | 3000.0000 | 3000.0000 |   1.44 GB |
| PubSubWithProducerInterceptorBenchmark | PubSubWithProducerInterceptor | 1000000      | True               | 934.5 ms | 14.15 ms | 13.24 ms | 174000.0000 | 3000.0000 | 3000.0000 |   1.53 GB |
| PubSubWithPublishInterceptorBenchmark  | PubSubWithPublishInterceptor  | 1000000      | True               | 930.6 ms | 14.29 ms | 13.37 ms | 174000.0000 | 3000.0000 | 3000.0000 |   1.53 GB |

```
// * Hints *

Outliers
  PubSubWithProducerInterceptorBenchmark.PubSubWithProducerInterceptor: MaxIterationCount=30, MaxWarmupIterationCount=10 -> 1 outlier  was  removed (840.20 ms)
  PubSubWithPublishInterceptorBenchmark.PubSubWithPublishInterceptor: MaxIterationCount=30, MaxWarmupIterationCount=10   -> 2 outliers were removed (862.16 ms, 863.90 ms)
  PubSubBenchmark.PubSub: MaxIterationCount=30, MaxWarmupIterationCount=10                                               -> 2 outliers were removed (810.49 ms, 823.65 ms)
  PubSubWithConsumerInterceptorBenchmark.PubSubWithConsumerInterceptor: MaxIterationCount=30, MaxWarmupIterationCount=10 -> 1 outlier  was  removed (947.16 ms)

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
```
