# SlimMessageBus roadmap

## Next patch

1. **Isolate consumer context per dispatch.** Fix concurrent and nested Memory transport sends that share a DI scope, and support dispatch from the root provider with scope validation enabled. Preserve the caller's scoped dependencies and verify constructor injection of `IConsumerContext`. [Concurrent dispatch #506](https://github.com/zarusz/SlimMessageBus/issues/506) · [Root-provider failure #410](https://github.com/zarusz/SlimMessageBus/issues/410)

## Next fixes and improvements

2. **Fix circuit breaker behavior for shared queues.** Reproduce the SQS report with two filtered consumers on one queue. Verify that opening the circuit stops new receives and that recovery resumes them. Check equivalent RabbitMQ behavior against the existing fix branch. [Circuit breaker #454](https://github.com/zarusz/SlimMessageBus/issues/454)
3. **Finish Kafka logger access.** Add an opt-in way for custom producer and consumer builder factories to receive the bus logger while preserving existing public factory signatures. [Kafka logging #466](https://github.com/zarusz/SlimMessageBus/issues/466) · [PR #467](https://github.com/zarusz/SlimMessageBus/pull/467)
4. **Support interceptors for batch publishing.** Define validation, outbox handling, and partial failures for collections while retaining bulk transport sends. [Batch publishing #425](https://github.com/zarusz/SlimMessageBus/issues/425)
5. **Add OpenTelemetry support.** Start with opt-in publish and consume spans, trace propagation, processing duration, and failure counts. Include failures that occur during deserialization. Prioritize instrumentation across existing transports before saga support or additional transports. [Tracing #149](https://github.com/zarusz/SlimMessageBus/issues/149) · [Metrics #333](https://github.com/zarusz/SlimMessageBus/issues/333)

## Issue cleanup

- The semaphore leak was fixed in 3.3.5 and confirmed by the reporter. Close the open issue with a link to the fix. [Issue #438](https://github.com/zarusz/SlimMessageBus/issues/438) · [PR #439](https://github.com/zarusz/SlimMessageBus/pull/439)
- RabbitMQ.Client 7 support shipped in 3.4.0. Close the open upgrade issue with a link to the implementation. [Issue #379](https://github.com/zarusz/SlimMessageBus/issues/379) · [PR #456](https://github.com/zarusz/SlimMessageBus/pull/456)
- MQTT wildcard support has merged. [PR #484](https://github.com/zarusz/SlimMessageBus/pull/484)
- The relational polling issue is closed. The change is in the 3.6.2 prerelease. [Issue #483](https://github.com/zarusz/SlimMessageBus/issues/483) · [3.6.2 release](https://github.com/zarusz/SlimMessageBus/releases/tag/3.6.2)
