# Release Notes of SlimMessageBus

## Version 0.9.6.28525

Fixes:
* Package SlimMessageBus.Host was not referenced properly by other packages. This is now fixed.
* SlimMessageBus.Host.Kafka: PendingRequestStore was introduced with in-memory/transient implementation that relays on a synchronized Dictionary<K, V>. Before it used to be ConcurrentDictionary, but there was some issues in one test environment. Furthermore the current approach should work better according to [this article](https://www.codeproject.com/Articles/548406/Dictionary-plus-Locking-versus-ConcurrentDictionar).

Packages:
* https://www.nuget.org/packages/SlimMessageBus.Host/0.9.6.28525
* https://www.nuget.org/packages/SlimMessageBus.Host.Kafka/0.9.6.28526
* https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.Json/0.9.6.28526
* https://www.nuget.org/packages/SlimMessageBus.Host.ServiceLocator/0.9.6.28526
