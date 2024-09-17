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
- [Consumers](#consumers)
  - [Offset Commit](#offset-commit)
  - [Consumer Error Handling](#consumer-error-handling)
  - [Debugging](#debugging)
- [Deployment](#deployment)

## Underlying client

The SMB Kafka implementation uses [confluent-kafka-dotnet](https://github.com/confluentinc/confluent-kafka-dotnet) .NET wrapper around the native [librdkafka](https://github.com/edenhill/librdkafka) library.

When troubleshooting or fine tuning it is worth reading the `librdkafka` and `confluent-kafka-dotnet` docs:

- [Introduction](https://github.com/confluentinc/librdkafka/blob/master/INTRODUCTION.md)
- [Broker version compatibility](https://github.com/confluentinc/librdkafka/blob/master/INTRODUCTION.md#broker-version-compatibility)
- [Using SSL with librdkafka](https://github.com/confluentinc/librdkafka/wiki/Using-SSL-with-librdkafka)

## Configuration properties

Producer, consumer and global configuration properties are described [here](https://github.com/confluentinc/librdkafka/blob/master/CONFIGURATION.md).
The configuration on the underlying Kafka client can be adjusted like so:

```cs
services.AddSlimMessageBus(mbb =>
{
   // ...
   mbb.WithProviderKafka(cfg =>
   {
      cfg.BrokerList = kafkaBrokers;
      cfg.ProducerConfig = (config) =>
      {
         // adjust the producer config
      };
      cfg.ConsumerConfig = (config) =>
      {
         // adjust the consumer config
      };
   });
});
```

### Minimizing message latency

There is a good description [here](https://github.com/confluentinc/librdkafka/wiki/How-to-decrease-message-latency) on improving the latency by applying producer/consumer settings on librdkafka. Here is how you enter the settings using SlimMessageBus:

```cs
services.AddSlimMessageBus(mbb =>
{
   mbb.WithProviderKafka(cfg =>
   {
      cfg.BrokerList = kafkaBrokers;

      cfg.ProducerConfig = (config) =>
      {
         config.LingerMs = 5; // 5ms
         config.SocketNagleDisable = true;
      };
      cfg.ConsumerConfig = (config) =>
      {
         config.FetchErrorBackoffMs = 1;
         config.SocketNagleDisable = true;
      };
   });
});
```

There is also a good discussion around latency in [this issue](https://github.com/confluentinc/confluent-kafka-dotnet/issues/89).

More documentation here:

- [How to decrease message latency](https://github.com/edenhill/librdkafka/wiki/How-to-decrease-message-latency)
- [Reduce latency](https://github.com/confluentinc/confluent-kafka-dotnet/wiki/Producing-messages#reduce-latency)

### SSL and password authentication

Example on how to configure SSL with SASL authentication (for cloudkarafka.com):

```cs
services.AddSlimMessageBus(mbb =>
{
   mbb.WithProviderKafka(cfg =>
   {
      cfg.BrokerList = kafkaBrokers;

      cfg.ProducerConfig = (config) =>
      {
         AddSsl(kafkaUsername, kafkaPassword, config);
      };
      cfg.ConsumerConfig = (config) =>
      {
         AddSsl(kafkaUsername, kafkaPassword, config);
      };
   });
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

Currently, [confluent-kafka-dotnet](https://github.com/confluentinc/confluent-kafka-dotnet) does not support custom partitioners (see [here](https://github.com/confluentinc/confluent-kafka-dotnet/issues/343)).
The default partitioner is supported, which works in this way:

- when message key is not provided then partition is assigned using round-robin,
- when message key is provided then same partition is assigned to same key

SMB Kafka allows to set a provider (selector) that will assign the message key for a given message and topic pair. Here is an example:

```cs
// MessageBusBuilder mbb;
mbb
   .Produce<MultiplyRequest>(x =>
   {
      x.DefaultTopic("topic1");
      // Message key could be set for the message
      x.KeyProvider((request, topic) => Encoding.ASCII.GetBytes((request.Left + request.Right).ToString()));
   })
   .WithProviderKafka(cfg => cfg.BrokerList = kafkaBrokers);
```

The key must be a `byte[]`.

### Assigning partition explicitly

SMB Kafka allows to set a provider (selector) that will assign the partition number for a given message and topic pair. Here is an example:

```cs
// MessageBusBuilder mbb;
mbb
   .Produce<PingMessage>(x =>
   {
      x.DefaultTopic("topic1");
      // Partition #0 for even counters
      // Partition #1 for odd counters
      x.PartitionProvider((message, topic) => message.Counter % 2);
   })
   .WithProviderKafka(cfg => cfg.BrokerList = kafkaBrokers);
```

With this approach your provider needs to know the number of partitions for a topic.

## Consumer context

The consumer can implement the `IConsumerWithContext` interface to access the Kafka native message:

```cs
public class PingConsumer : IConsumer<PingMessage>, IConsumerWithContext
{
   public IConsumerContext Context { get; set; }

   public Task OnHandle(PingMessage message)
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
By default the [DefaultKafkaHeaderSerializer](../src/SlimMessageBus.Host.Kafka/DefaultKafkaHeaderSerializer.cs) is used to serialize header values.
If you need to specify a different serializer provide a specfic `IMessageSerializer` implementation (custom or one of the available serialization plugins):

```cs
// MessageBusBuilder mbb;
mbb
   .WithProviderKafka(cfg =>
   {
      cfg.BrokerList = kafkaBrokers;
      cfg.HeaderSerializer = new DefaultKafkaHeaderSerializer() // specify a different header values serializer
   });
```

> Since version 2.0.0, uses the [DefaultKafkaHeaderSerializer](../src/SlimMessageBus.Host.Kafka/DefaultKafkaHeaderSerializer.cs) serializer which converts the passed values into string. Prior version 2.0.0, by default the same serializer for the bus was used to also serialize message header values.

## Consumers

### Offset Commit

In the current Kafka provider implementation, SMB handles the manual commit of topic-partition offsets for the consumer.Th
is configuration is controlled through the following methods on the consumer builder:

- `CheckpointEvery(int)` – Commits the offset after a specified number of processed messages.
- `CheckpointAfter(TimeSpan)` – Commits the offset after a specified time interval.

Here’s an example:

@[:cs](../src/Tests/SlimMessageBus.Host.Kafka.Test/KafkaMessageBusIt.cs,ExampleCheckpointConfig)

In future versions, the following features may be added:

- Allowing users to manage offset commits themselves (manual commit, fully controlled by the library users).
- Providing the option to use Kafka's automatic offset commit to reduce latency.

### Consumer Error Handling

When an error occurs during message processing, SMB attempts to resolve a [custom error handler](intro.md#custom-consumer-error-handler) to handle the exception.

The error handler can perform the following actions:

- Retry message processing a specified number of times.
- If the retry limit is exceeded, forward the message to a designated topic for failed messages (Dead Letter Queue).
- Stop the consumer's message processing (currently not supported), though the circuit breaker feature from [this pull request](https://github.com/zarusz/SlimMessageBus/pull/282) could be used as an alternative.

If no custom error handler is provided, the provider logs the exception and moves on to process the next message.

### Debugging

Kafka uses a sophisticated protocol for partition assignment:

- Partition assignments may change due to factors like rebalancing.
- A running consumer instance might not receive any partitions if there are more consumers than partitions for a given topic.

To better understand what's happening, you can enable Debug level logging in your library, such as SlimMessageBus.Host.Kafka.KafkaGroupConsumer.

At this logging level, you can track the lifecycle events of the consumer group:

```
[00:03:06 INF] SlimMessageBus.Host.Kafka.KafkaGroupConsumer Group [subscriber]: Subscribing to topics: 4p5ma6io-test-ping
[00:03:06 INF] SlimMessageBus.Host.Kafka.KafkaGroupConsumer Group [subscriber]: Consumer loop started
[00:03:12 DBG] SlimMessageBus.Host.Kafka.KafkaGroupConsumer Group [subscriber]: Assigned partition, Topic: 4p5ma6io-test-ping, Partition: [0]
[00:03:12 INF] SlimMessageBus.Host.Kafka.KafkaPartitionConsumer Creating consumer for Group: subscriber, Topic: 4p5ma6io-test-ping, Partition: [0]
[00:03:12 DBG] SlimMessageBus.Host.Kafka.KafkaGroupConsumer Group [subscriber]: Assigned partition, Topic: 4p5ma6io-test-ping, Partition: [1]
[00:03:12 INF] SlimMessageBus.Host.Kafka.KafkaPartitionConsumer Creating consumer for Group: subscriber, Topic: 4p5ma6io-test-ping, Partition: [1]
...
[00:03:15 DBG] SlimMessageBus.Host.Kafka.KafkaGroupConsumer Group [subscriber]: Received message with Topic: 4p5ma6io-test-ping, Partition: [1], Offset: 98578, payload size: 57
[00:03:15 INF] SlimMessageBus.Host.Kafka.Test.KafkaMessageBusIt.PingConsumer Got message 073 on topic 4p5ma6io-test-ping.
[00:03:15 DBG] SlimMessageBus.Host.Kafka.KafkaGroupConsumer Group [subscriber]: Received message with Topic: 4p5ma6io-test-ping, Partition: [1], Offset: 98579, payload size: 57
[00:03:15 INF] SlimMessageBus.Host.Kafka.Test.KafkaMessageBusIt.PingConsumer Got message 075 on topic 4p5ma6io-test-ping.
[00:03:16 DBG] SlimMessageBus.Host.Kafka.KafkaGroupConsumer Group [subscriber]: Reached end of partition, Topic: 4p5ma6io-test-ping, Partition: [0], Offset: 100403
[00:03:16 DBG] SlimMessageBus.Host.Kafka.KafkaGroupConsumer Group [subscriber]: Commit Offset, Topic: 4p5ma6io-test-ping, Partition: [0], Offset: 100402
[00:03:16 DBG] SlimMessageBus.Host.Kafka.KafkaGroupConsumer Group [subscriber]: Reached end of partition, Topic: 4p5ma6io-test-ping, Partition: [1], Offset: 98580
[00:03:16 DBG] SlimMessageBus.Host.Kafka.KafkaGroupConsumer Group [subscriber]: Commit Offset, Topic: 4p5ma6io-test-ping, Partition: [1], Offset: 98579
```

## Deployment

The `librdkafka` distribution for Windows requires [Visual C++ Redistributable for 2013](https://www.microsoft.com/en-US/download/details.aspx?id=40784) installed on the server. More information can be found [here](https://www.microsoft.com/en-US/download/details.aspx?id=40784).
