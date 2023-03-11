# Serialization plugins for SlimMessageBus <!-- omit in toc -->

Please read the [Introduction](intro.md) before reading this provider documentation.

- [Configuration](#configuration)
- [Json (Newtonsoft.Json)](#json-newtonsoftjson)
- [Json (System.Text.Json)](#json-systemtextjson)
- [Avro](#avro)
- [GoogleProtobuf](#googleprotobuf)
- [Hybrid](#hybrid)

## Configuration

Message serializers implement the interface [IMessageSerializer](../src/SlimMessageBus.Host.Serialization/IMessageSerializer.cs).
There are few plugins to choose from.

We register the serializer service in the DI:

```cs
services.AddSlimMessageBus(mbb => 
{
   // Use JSON for message serialization
   mbb.AddJsonSerializer(); // requires SlimMessageBus.Host.Json or SlimMessageBus.Host.SystemTextJson package
});
```

If the bus is a Hybrid bus composed of other child buses, then we can register multiple serializers and instuct which serializer type to apply for the given child bus.
Consider the following example:

```cs
services.AddSlimMessageBus(mbb => 
{
   // Use JSON for message serialization
   mbb.AddJsonSerializer(); // requires SlimMessageBus.Host.Serialization.Json or SlimMessageBus.Host.Serialization.SystemTextJson package

   // Use Protobuf for message serialization
   mbb.AddGoogleProtobufSerializer(); // requires SlimMessageBus.Host.Serialization.GoogleProtobuf package

   mbb.AddChildBus("Kafka", builder =>
   {
      builder.WithProviderKafka(/*...*/);
      builder.WithSerializer<GoogleProtobufMessageSerializer>(); // this child bus will use a specific serializer
   });
   mbb.AddChildBus("AzureSB", builder =>
   {
      builder.WithProviderServiceBus(/*...*/);
      // this child bus will use the default serializer (IMessageSerializer) - which is the first one registered in DI (here Json)
      //builder.WithSerializer<IMessageSerializer>();
   });
});
```

> The serializer will be a singleton used across all the concurently running tasks of producing and consuming messages in a given bus instance. The serializers are designed, so that they are Thread-safe.

## Json (Newtonsoft.Json)

Nuget package: [SlimMessageBus.Host.Serialization.Json](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.Json)

The Json plugin brings in JSON serialization using the [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json) library.

To use it install the nuget package `SlimMessageBus.Host.Serialization.Json` and then configure the bus:

```cs
mbb.AddJsonSerializer();
```

This will apply the `Newtonsoft.Json` default serialization settings and will use `UTF8` encoding for converting `string` to `byte[]`.

In order to customize the JSON formatting use the additional parameters:

```cs
var jsonSerializerSettings = new Newtonsoft.Json.JsonSerializerSettings
{
   TypeNameHandling = Newtonsoft.Json.TypeNameHandling.Objects
};

mbb.AddJsonSerializer(jsonSerializerSettings, Encoding.UTF8);
```

## Json (System.Text.Json)

Nuget package: [SlimMessageBus.Host.Serialization.SystemTextJson](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.SystemTextJson)

The Json plugin brings in JSON serialization using the [System.Text.Json](https://www.nuget.org/packages/System.Text.Json) library.

To use it install the nuget package `SlimMessageBus.Host.Serialization.SystemTextJson` and then configure the bus similar to Json above.

```cs
mbb.AddJsonSerializer();
```

One can customize or override the `JsonSerializerOptions`:

```cs
// Configure JSON options in the MSDI
services.AddTransient(new JsonSerializerOptions { /* ... */ })

// Then
services.AddSlimMessageBus(mbb => 
{
   mbb.AddJsonSerializer();   
});

// Or provide an JSON options directly
services.AddSlimMessageBus(mbb => 
{
   mbb.AddJsonSerializer(new JsonSerializerOptions());   
});
```

By default the plugin adds a custom converter (see [`ObjectToInferredTypesConverter`](../src/SlimMessageBus.Host.Serialization.SystemTextJson/ObjectToInferredTypesConverter.cs)) that infers primitive types whenever the type to deseriaize is object (unknown). This helps with header value serialization for transport providers that transmit the headers as binary (Kafka). See the source code for better explanation.

## Avro

Nuget package: [SlimMessageBus.Host.Serialization.Avro](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.Avro)

The Avro plugin brings in the [Apache Avro](https://avro.apache.org/) binary serialization using the [Apache.Avro](https://www.nuget.org/packages/Apache.Avro/) library.

To use it install the nuget package `SlimMessageBus.Host.Serialization.Avro` and then configure the bus:

```cs
mbb.AddAvroSerializer();
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
mbb.AddAvroSerializer(mf, sl);
```

The default `AvroMessageSerializer` constructor will use the `ReflectionMessageCreationStategy` and `ReflectionSchemaLookupStrategy` strategies. While these are slower bacause of usage of reflection, it is certainly more convenient to use.

## GoogleProtobuf

The GoogleProtobuf plugin brings in Protobuf serialization using the [Google.Protobuf](https://www.nuget.org/packages/Google.Protobuf) library.

Nuget package: [SlimMessageBus.Host.Serialization.GoogleProtobuf](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.GoogleProtobuf)

To use it install the nuget package `SlimMessageBus.Host.Serialization.GoogleProtobuf` and then configure the bus:

```cs
mbb.AddGoogleProtobufSerializer();
```

This will apply the `Google.Protobuf` default serialization settings for converting `IMessage` to `byte[]`.

## Hybrid

Nuget package: [SlimMessageBus.Host.Serialization.Hybrid](https://www.nuget.org/packages/SlimMessageBus.Host.Serialization.Hybrid)

The Hybrid plugin allows to have multiple serialization formats on one message bus and delegate (route) message serialization (and deserialization) to other serialization plugins.

To use it install the nuget package `SlimMessageBus.Host.Serialization.Hybrid` and then configure the bus:

```cs
services.AddSlimMessageBus(mbb => 
{
   // serializer 1
   var avroSerializer = new AvroMessageSerializer();

   // serializer 2
   var jsonSerializer = new JsonMessageSerializer();

   // Note: Certain messages will be serialized by the Avro serializer, other using the Json serializer
   mbb.AddHybridSerializer(new Dictionary<IMessageSerializer, Type[]>
   {
      [jsonSerializer] = new[] { typeof(SubtractCommand) }, // the first one will be the default serializer, no need to declare types here
      [avroSerializer] = new[] { typeof(AddCommand), typeof(MultiplyRequest), typeof(MultiplyResponse) },
   }, defaultMessageSerializer: jsonSerializer);
});
```

The routing to the proper serializer happens based on message type. When a type cannot be matched the default serializer will be used.
