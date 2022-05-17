# Apache Kafka Provider for SlimMessageBus <!-- omit in toc -->

Please read the [Introduction](intro.md) before reading this provider documentation.

- [Underlying client](#underlying-client)
- [Configuration properties](#configuration-properties)
  - [Minimizing message latency](#minimizing-message-latency)
  - [SSL and password authentication](#ssl-and-password-authentication)
- [Selecting message partition for topic producer](#selecting-message-partition-for-topic-producer)
  - [Default partitioner with message key](#default-partitioner-with-message-key)
  - [Assigning partition explicitly](#assigning-partition-explicitly)
- [Consumer context](#consumer-context)
- [Message Headers](#message-headers)
- [Deployment](#deployment)

## Underlying client

The SMB Kafka implementation uses [confluent-kafka-dotnet](https://github.com/confluentinc/confluent-kafka-dotnet) .NET wrapper around the native [librdkafka](https://github.com/edenhill/librdkafka) library.

When troubleshooting or fine tuning it is worth reading the `librdkafka` and `confluent-kafka-dotnet` docs:

- [Introduction](https://github.com/edenhill/librdkafka/blob/master/INTRODUCTION.md
- [Broker version compatibility](https://github.com/edenhill/librdkafka/wiki/Broker-version-compatibility)
- [Using SSL with librdkafka](https://github.com/edenhill/librdkafka/wiki/Using-SSL-with-librdkafka)

## Configuration properties

Producer, consumer and global configuration properties are described [here](https://github.com/edenhill/librdkafka/blob/master/CONFIGURATION.md).
The configuration on the underlying Kafka client can be adjusted like so:

```cs
// MessageBusBuilder mbb;
mbb.    
   // ...
   .WithProviderKafka(new KafkaMessageBusSettings(kafkaBrokers)
   {
      ProducerConfig = (config) =>
      {
         // adjust the producer config
      },
      ConsumerConfig = (config) => 
      {
         // adjust the consumer config
      }
   });
```

### Minimizing message latency

There is a good description [here](https://github.com/edenhill/librdkafka/wiki/How-to-decrease-message-latency) on improving the latency by applying producer/consumer settings on librdkafka. Here is how you enter the settings using SlimMessageBus:

```cs
// MessageBusBuilder mbb;
mbb.    
   // ...
   .WithProviderKafka(new KafkaMessageBusSettings(kafkaBrokers)
   {
      ProducerConfig = (config) =>
      {
         config.LingerMs = 5; // 5ms
         config.SocketNagleDisable = true;
      },
      ConsumerConfig = (config) => 
      {
         config.FetchErrorBackoffMs = 1;
         config.SocketNagleDisable = true;
      }
   });
```

There is also a good discussion around latency in [this issue](https://github.com/confluentinc/confluent-kafka-dotnet/issues/89).

More documentation here:
- [How to decrease message latency](https://github.com/edenhill/librdkafka/wiki/How-to-decrease-message-latency)
- [Reduce latency](https://github.com/confluentinc/confluent-kafka-dotnet/wiki/Producing-messages#reduce-latency)

### SSL and password authentication

Example on how to configure SSL with SASL authentication (for cloudkarafka.com):

```cs
// MessageBusBuilder mbb;
mbb.    
   // ...
   .WithProviderKafka(new KafkaMessageBusSettings(kafkaBrokers)
   {
      ProducerConfig = (config) =>
      {
         AddSsl(kafkaUsername, kafkaPassword, config);
      },
      ConsumerConfig = (config) => 
      {
         AddSsl(kafkaUsername, kafkaPassword, config);
      }
   });
```

```cs
private static void AddSsl(string username, string password, ClientConfig c)
{
   c.SecurityProtocol = SecurityProtocol.SaslSsl;
   c.SaslUsername = username;
   c.SaslPassword = password;
   c.SaslMechanism = SaslMechanism.ScramSha256;
   c.SslCaLocation = "cloudkarafka_2020-12.ca";
}
```

The file `cloudkarafka_2020-12.ca` has to be set to `Copy to Output Directory` as `Copy always`.

## Selecting message partition for topic producer

Kafka topics are broken into partitions. The question is how does SMB Kafka choose the partition to assign the message?
There are two possible options:

### Default partitioner with message key

Currently the [confluent-kafka-dotnet](https://github.com/confluentinc/confluent-kafka-dotnet) does not support custom partitioners (see [here](https://github.com/confluentinc/confluent-kafka-dotnet/issues/343)).
The default partitioner is supported, which works in this way:

- when message key is not provided then partition is assigned using round-robin,
- when message key is provided then same partition is assigned to same key

SMB Kafka allows to set a provider (selector) that will assign the message key for a given message and topic pair. Here is an example:

```cs
// MessageBusBuilder mbb;
mbb.    
   .Produce<MultiplyRequest>(x => 
   {
      x.DefaultTopic("topic1");
      // Message key could be set for the message
      x.KeyProvider((request, topic) => Encoding.ASCII.GetBytes((request.Left + request.Right).ToString()));
   })
   .WithProviderKafka(new KafkaMessageBusSettings(kafkaBrokers))
   .Build();
```

The key must be a `byte[]`.

### Assigning partition explicitly

SMB Kafka allows to set a provider (selector) that will assign the partition number for a given message and topic pair. Here is an example:

```cs
// MessageBusBuilder mbb;
mbb.    
   .Produce<PingMessage>(x =>
   {
      x.DefaultTopic("topic1");
      // Partition #0 for even counters
      // Partition #1 for odd counters
      x.PartitionProvider((message, topic) => message.Counter % 2);
   })
   .WithProviderKafka(new KafkaMessageBusSettings(kafkaBrokers));
```

With this approach your provider needs to know the number of partitions for a topic.

## Consumer context

The consumer can implement the `IConsumerWithContext` interface to access the Kafka native message:

```cs
public class PingConsumer : IConsumer<PingMessage>, IConsumerWithContext
{
   public IConsumerContext Context { get; set; }

   public Task OnHandle(PingMessage message, string path)
   {
      // SMB Kafka transport specific extension:
      var transportMessage = Context.GetTransportMessage();
      var partition = transportMessage.TopicPartition.Partition;
   }
}
```

This could be useful to extract the message's offset or partition.

## Message Headers

SMB uses headers to pass additional metadata information with the message. This includes the `MessageType` (of type `string`) or in the case of request/response messages the `RequestId` (of type `string`), `ReplyTo` (of type `string`) and `Expires` (of type `long`).

The Kafka message header values are natively binary (`byte[]`) in the underlying .NET client, as a result SMB needs to serialize the header values.
By default the same serializer is used to serialize header values as is being used for the message serialization.
If you need to specify a different serializer provide a specfic `IMessageSerializer` implementation (custom or one of the available serialization plugins):

```cs
// MessageBusBuilder mbb;
mbb.    
   .WithProviderKafka(new KafkaMessageBusSettings(kafkaBrokers)
   {
      HeaderSerializer = new JsonMessageSerializer() // specify a different header values serializer
   });
```

> By default for header serialization (if not specified) SMB Kafka uses the same serializer that was set for the bus.

## Deployment

The `librdkafka` distribution for Windows requires [Visual C++ Redistributable for 2013](https://www.microsoft.com/en-US/download/details.aspx?id=40784) installed on the server. More information can be found [here](https://www.microsoft.com/en-US/download/details.aspx?id=40784).
