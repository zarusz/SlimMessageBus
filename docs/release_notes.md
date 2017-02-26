# Release Notes of SlimMessageBus

## Version 0.9.8.X

Features:
* The core IRequestResponseBus methods now take an optional [CancellationToken](https://msdn.microsoft.com/en-us/library/system.threading.cancellationtoken(v=vs.110).aspx). This allows for the API client to cancel the pending request. For example you can use this with [WebApi async actions](http://www.davepaquette.com/archive/2015/07/19/cancelling-long-running-queries-in-asp-net-mvc-and-web-api.aspx), in the case where the HTTP call is cancelled.


Packages:
* https://www.nuget.org/packages/SlimMessageBus/0.9.8.X
* https://www.nuget.org/packages/SlimMessageBus.Host/0.9.8.X
* https://www.nuget.org/packages/SlimMessageBus.Host.Kafka/0.9.8.X

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
