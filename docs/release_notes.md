# Release Notes of SlimMessageBus

## Version 0.9.11
Date: 2017-05-08

Features:
* SlimMessageBus.Host.Autofac: The Autofac plugin assembly was introduced.
* Minor changes and improvements.

Packages:
* https://www.nuget.org/packages/SlimMessageBus.Host/0.9.11
* https://www.nuget.org/packages/SlimMessageBus.Host.ServiceLocator/0.9.11
* https://www.nuget.org/packages/SlimMessageBus.Host.Autofac/0.9.11
* https://www.nuget.org/packages/SlimMessageBus.Host.Kafka/0.9.11

## Version 0.9.10
Date: 2017-04-25

Features:
* Dropping build number from version names.
* SlimMessageBus.Host.Kafka:
	* Targeting Kafka client version 0.9.5 that fixes high CPU usage [bug](https://github.com/confluentinc/confluent-kafka-dotnet/issues/87). 
	* Improving debug logging.

Packages:
* https://www.nuget.org/packages/SlimMessageBus/0.9.10
* https://www.nuget.org/packages/SlimMessageBus.Host/0.9.10
* https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.Json/0.9.10
* https://www.nuget.org/packages/SlimMessageBus.Host.ServiceLocator/0.9.10
* https://www.nuget.org/packages/SlimMessageBus.Host.Kafka/0.9.10

## Version 0.9.9.16686
Date: 2017-04-14

Features:
* SlimMessageBus.Host.Kafka: Switched the kafka client to [confluent-kafka-dotnet](https://github.com/confluentinc/confluent-kafka-dotnet) which is an evolution of the previously used ([rdkafka-dotnet](https://github.com/ah-/rdkafka-dotnet)).
* SlimMessageBus.Host.Kafka: Added factory methods to KafkaMessageBusSettings that allows to customize settings of producer/consumer in the underlying kafka client.

Known Bugs:
* SlimMessageBus.Host.Kafka: There is a [bug](https://github.com/confluentinc/confluent-kafka-dotnet/issues/87) with the underlying client that causes high CPU usage. When fix is made available, a new package SlimMessageBus.Host.Kafka will be released that targets newer client version.

Packages:
* https://www.nuget.org/packages/SlimMessageBus/0.9.8.16686
* https://www.nuget.org/packages/SlimMessageBus.Host/0.9.8.16686
* https://www.nuget.org/packages/SlimMessageBus.Host.Kafka/0.9.9.16686
* https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.Json/0.9.6.16686
* https://www.nuget.org/packages/SlimMessageBus.Host.ServiceLocator/0.9.6.16686

## Version 0.9.8.34503

Features:
* The core IRequestResponseBus methods now take an optional [CancellationToken](https://msdn.microsoft.com/en-us/library/system.threading.cancellationtoken(v=vs.110).aspx). This allows for the API client to cancel the pending request. For example you can use this with [WebApi async actions](http://www.davepaquette.com/archive/2015/07/19/cancelling-long-running-queries-in-asp-net-mvc-and-web-api.aspx), in the case where the HTTP call is cancelled.
* SlimMessageBus assembly now targets Framework 4.0. This will make older project able to use this library.

Packages:
* https://www.nuget.org/packages/SlimMessageBus/0.9.8.34503
* https://www.nuget.org/packages/SlimMessageBus.Host/0.9.8.34503
* https://www.nuget.org/packages/SlimMessageBus.Host.Kafka/0.9.8.34503

## Version 0.9.7.41418

Features:
* Added RequestHandlerFaultedMessageBusException: When request handler errors out the IRequestResponseBus.Send() resolves to RequestHandlerFaultedMessageBusException
* Added code comments

Packages:
* https://www.nuget.org/packages/SlimMessageBus/0.9.7.41418
* https://www.nuget.org/packages/SlimMessageBus.Host/0.9.7.41418
* https://www.nuget.org/packages/SlimMessageBus.Host.Kafka/0.9.7.41418

## Version 0.9.6.28525

Fixes:
* Package SlimMessageBus.Host was not referenced properly by other packages. This is now fixed.
* SlimMessageBus.Host.Kafka: PendingRequestStore was introduced with in-memory/transient implementation that relays on a synchronized Dictionary<K, V>. Before it used to be ConcurrentDictionary, but there was some issues in one test environment. Furthermore the current approach should work better according to [this article](https://www.codeproject.com/Articles/548406/Dictionary-plus-Locking-versus-ConcurrentDictionar).

Packages:
* https://www.nuget.org/packages/SlimMessageBus.Host/0.9.6.28525
* https://www.nuget.org/packages/SlimMessageBus.Host.Kafka/0.9.6.28526
* https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.Json/0.9.6.28526
* https://www.nuget.org/packages/SlimMessageBus.Host.ServiceLocator/0.9.6.28526
