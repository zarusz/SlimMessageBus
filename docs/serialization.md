# Serialization plugins for SlimMessageBus <!-- omit in toc -->

Please read the [Introduction](intro.md) before reading this provider documentation.

- [Configuration](#configuration)
- [Json](#json)
- [Avro](#avro)
- [GoogleProtobuf](#googleprotobuf)
- [Hybrid](#hybrid)

## Configuration

Part of message bus configuration is choosing the serialization plugin:

```cs
// Use JSON for message serialization
IMessageSerializer serializer = new JsonMessageSerializer();

// MessageBusBuilder mbb;
mbb.    
   .WithSerializer(serializer)
```

> One serializer instance will be used across all the concurently running tasks of producing and consuming messages in a given bus instance. The serializers are designed, so that they are Thread-safe.

## Json

Nuget package: [SlimMessageBus.Host.Serialization.Json](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.Json)

The Json plugin brings in JSON serialization using the [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json) library.

To use it install the nuget package `SlimMessageBus.Host.Serialization.Json` and then configure the bus:

```cs
var jsonSerializer = new JsonMessageSerializer();
mbb.WithSerializer(jsonSerializer);
```

This will apply the `Newtonsoft.Json` default serialization settings and will use `UTF8` encoding for converting `string` to `byte[]`.

In order to customize how messages are formatted use the alternative constructor:

```cs
var jsonSerializerSettings = new Newtonsoft.Json.JsonSerializerSettings
   {
      TypeNameHandling = Newtonsoft.Json.TypeNameHandling.Objects
   };

var jsonSerializer = new JsonMessageSerializer(jsonSerializerSettings, Encoding.UTF8)
```

## Avro

Nuget package: [SlimMessageBus.Host.Serialization.Avro](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.Avro)

The Avro plugin brings in the [Apache Avro](https://avro.apache.org/) binary serialization using the [Apache.Avro](https://www.nuget.org/packages/Apache.Avro/) library.

To use it install the nuget package `SlimMessageBus.Host.Serialization.Avro` and then configure the bus:

```cs
var avroSerializer = new AvroMessageSerializer();
mbb.WithSerializer(avroSerializer);
```

The Apache.Avro library requires each of your serialized messages to implement the interface `Avro.Specific.ISpecificRecord`. That interface requires to provide the `Avro.Schema` object as well as is responsible for serializing and deserializing the message.

The typical approach for working with Avro is to create the contract first using the [Avro IDL contract](https://avro.apache.org/docs/current/idl.html) and then generating the respective C# classes that represent messages. The sample [Sample.Serialization.MessagesAvro](../src/Samples/Sample.Serialization.MessagesAvro) shows how to generate C# classes having the IDL contract. Consult the sample for more details. 

There are ways to customize the `AvroMessageSerializer` by providing strategies for message creation and Avro schema lookup (for reader and writer):

- Since performance is key when choosing Avro for serialization, the Apache.Avro library allows for message reuse (to avoid GC and heap allocation). SMB plugin provides a way to select the strategy for message creation.
- The Apache.Avro library requires the Avro.Schema for each message and for the read or write case. This allows for schema versioning and is specific to Avro.

The example shows how to use a different strategy:

```cs
var sl = new DictionarySchemaLookupStrategy();
// register all your types
sl.Add(typeof(AddCommand), AddCommand._SCHEMA);
sl.Add(typeof(MultiplyRequest), MultiplyRequest._SCHEMA);
sl.Add(typeof(MultiplyResponse), MultiplyResponse._SCHEMA);

var mf = new DictionaryMessageCreationStategy();
// register all your types
mf.Add(typeof(AddCommand), () => new AddCommand());
mf.Add(typeof(MultiplyRequest), () => new MultiplyRequest());
mf.Add(typeof(MultiplyResponse), () => new MultiplyResponse());
   
// longer approach, but should be faster as it's not using reflection
var avroSerializer = new AvroMessageSerializer(mf, sl);
```

The default `AvroMessageSerializer` constructor will use the `ReflectionMessageCreationStategy` and `ReflectionSchemaLookupStrategy` strategies. While these are slower bacause of usage of reflection, it is certainly more convenient to use.

## GoogleProtobuf

The GoogleProtobuf plugin brings in Protobuf serialization using the [Google.Protobuf](https://www.nuget.org/packages/Google.Protobuf) library.

Nuget package: [SlimMessageBus.Host.Serialization.GoogleProtobuf](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.GoogleProtobuf)

To use it install the nuget package `SlimMessageBus.Host.Serialization.GoogleProtobuf` and then configure the bus:

```cs
var googleProtobufMessageSerializer = new GoogleProtobufMessageSerializer();
mbb.WithSerializer(googleProtobufMessageSerializer);
```

This will apply the `Google.Protobuf` default serialization settings for converting `IMessage` to `byte[]`.

## Hybrid

Nuget package: [SlimMessageBus.Host.Serialization.Hybrid](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.Hybrid)

The Hybrid plugin allows to have multiple serialization formats on one message bus and delegate (route) message serialization (and deserialization) to other serialization plugins.

To use it install the nuget package `SlimMessageBus.Host.Serialization.Hybrid` and then configure the bus:

```cs
var avroSerializer = new AvroMessageSerializer();
var jsonSerializer = new JsonMessageSerializer();

// Note: Certain messages will be serialized by one Avro serializer, other using the Json serializer
var hybridSerializer = new HybridMessageSerializer(new Dictionary<IMessageSerializer, Type[]>
{
   [jsonSerializer] = new[] { typeof(SubtractCommand) }, // the first one will be the default serializer, no need to declare types here
   [avroSerializer] = new[] { typeof(AddCommand), typeof(MultiplyRequest), typeof(MultiplyResponse) },
});

mbb.WithSerializer(hybridSerializer);
```

Currently the routing happens based on message type only.
