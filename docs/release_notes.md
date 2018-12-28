**This page is deprecated.**
Use the GitHub [releases](https://github.com/zarusz/SlimMessageBus/releases) page.

# Release Notes of SlimMessageBus

## Version 1.1.0
Date: 2018-12-09

Features:
* Introduced Memory provider for in-process communication.
* Introduced AspNetCore extension that uses the ASP.NET Core 2 dependency injection.
* Refactoring around fluent configurations (moved Group to Kafka/AzureEventHub providers).

Packages:
* https://www.nuget.org/packages/SlimMessageBus/1.0.1
* https://www.nuget.org/packages/SlimMessageBus.Host/1.1.0
* https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.Json/1.1.0
* https://www.nuget.org/packages/SlimMessageBus.Host.ServiceLocator/1.1.0
* https://www.nuget.org/packages/SlimMessageBus.Host.Autofac/1.1.0
* https://www.nuget.org/packages/SlimMessageBus.Host.Unity/1.1.0
* https://www.nuget.org/packages/SlimMessageBus.Host.Memory/1.1.0
* https://www.nuget.org/packages/SlimMessageBus.Host.AspNetCore/1.1.0

## Version 1.0.1
Date: 2018-03-12

Features:
* Updating package metadata (license, tags, description, etc).

Packages:
* https://www.nuget.org/packages/SlimMessageBus/1.0.1
* https://www.nuget.org/packages/SlimMessageBus.Host/1.0.1
* https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.Json/1.0.1
* https://www.nuget.org/packages/SlimMessageBus.Host.ServiceLocator/1.0.1
* https://www.nuget.org/packages/SlimMessageBus.Host.Autofac/1.0.1
* https://www.nuget.org/packages/SlimMessageBus.Host.Unity/1.0.1
* https://www.nuget.org/packages/SlimMessageBus.Host.Kafka/1.0.1
* https://www.nuget.org/packages/SlimMessageBus.Host.AzureEventHub/1.0.1

## Version 1.0.0
Date: 2018-03-04

Features:
* Porting the project to .NET Standard and samples to .NET Core 2.0

Packages:
* https://www.nuget.org/packages/SlimMessageBus/1.0.0
* https://www.nuget.org/packages/SlimMessageBus.Host/1.0.0
* https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.Json/1.0.0
* https://www.nuget.org/packages/SlimMessageBus.Host.ServiceLocator/1.0.0
* https://www.nuget.org/packages/SlimMessageBus.Host.Autofac/1.0.0
* https://www.nuget.org/packages/SlimMessageBus.Host.Unity/1.0.0
* https://www.nuget.org/packages/SlimMessageBus.Host.Kafka/1.0.0
* https://www.nuget.org/packages/SlimMessageBus.Host.AzureEventHub/1.0.0

## Version 0.10.1
Date: 2018-02-26

Features:
* SlimMessageBus.Host.Unity
  * Provider for Unity container by @diseks

Packages:
* https://www.nuget.org/packages/SlimMessageBus.Host.Unity/0.10.1

## Version 0.10.1
Date: 2018-02-23

Features:
* SlimMessageBus.Host.Kafka:
  * Introducing partition selector.
  * Introducing message key provider.
* SlimMessageBus.Host.AzureEventHub:
  * Released a stable version.
* SlimMessageBus.Host
  * Changes to support Kafka partition selector.

Packages:
* https://www.nuget.org/packages/SlimMessageBus.Host/0.10.1
* https://www.nuget.org/packages/SlimMessageBus.Host.Kafka/0.10.1
* https://www.nuget.org/packages/SlimMessageBus.Host.AzureEventHub/0.10.1

## Version 0.9.15
Date: 2017-09-30

Features:
* SlimMessageBus.Host.Kafka:
  * Upgraded Confluent.Kafka from version 0.9.5 to 0.11.0.
  * Bugfix: When publish failed no error was reported to client code. Now exception will be thrown.
  * Internal refactoring and improvements.
* Minor changes and improvements (mostly refactoring).

Packages:
* https://www.nuget.org/packages/SlimMessageBus.Host/0.9.15
* https://www.nuget.org/packages/SlimMessageBus.Host.Kafka/0.9.15
* https://www.nuget.org/packages/SlimMessageBus.Host.AzureEventHub/0.9.15-alpha2

## Version 0.9.13
Date: 2017-07-15

Features:
* SlimMessageBus.Host.AzureEventHub: The [Azure Event Hub](https://docs.microsoft.com/en-us/azure/event-hubs/) provider was introduced. This is the very first alpha release.
* Minor changes and improvements.

Packages:
* https://www.nuget.org/packages/SlimMessageBus.Host/0.9.13
* https://www.nuget.org/packages/SlimMessageBus.Host.Kafka/0.9.13
* https://www.nuget.org/packages/SlimMessageBus.Host.AzureEventHub/0.9.13-alpha1

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
