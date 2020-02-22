# Serialization plugins for SlimMessageBus

If you are new to SMB, consider reading the [Introduction](intro.md) documentation first.

## Introduction

Part of message bus configuration is choosing the serializaion plugin:

```cs
// Use JSON for message serialization
IMessageSerializer serializer = new JsonMessageSerializer();

IMessageBus bus = MessageBusBuilder
   .Create()
   .WithSerializer(serializer)
   .Build();
```

> One serializer instance will be used across all the concurently running tasks of producing and consuming messages in a given bus instance. The serializers are designed, so that they are Thread-safe.

## Json

Nuget package: [SlimMessageBus.Host.Serialization.Json](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.Json)

The Json plugin brings in JSON serialization using the [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json) library.

To use it install the nuget package `SlimMessageBus.Host.Serialization.Json` and then configure the bus:

```cs
var serializer = new JsonMessageSerializer();
```

This will apply the `Newtonsoft.Json` default serialization settings and will use `UTF8` encoding for converting `string` to `byte[]`. 

In order to customize how messages are formatted, use the alternative constructor:

```cs
var jsonSerializerSettings = new Newtonsoft.Json.JsonSerializerSettings
   {
      TypeNameHandling = Newtonsoft.Json.TypeNameHandling.Objects
   };

var serializer = new JsonMessageSerializer(jsonSerializerSettings, Encoding.UTF8)
```

## Avro

Nuget package: [SlimMessageBus.Host.Serialization.Avro](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.Avro)

> ToDo

## Hybrid

Nuget package: [SlimMessageBus.Host.Serialization.Hybrid](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.Hybrid)

> ToDo

