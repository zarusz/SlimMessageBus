# Apache Kafka Provider for SlimMessageBus

## Introduction

Please read the [Introduction](intro.md) before reading this provider documentation.

### Underlying client

The SMB Kafka implementation uses [confluent-kafka-dotnet](https://github.com/confluentinc/confluent-kafka-dotnet) .NET wrapper around the native [librdkafka](https://github.com/edenhill/librdkafka) library.

When troubleshooting or fine tuning it is worth reading the `librdkafka` and `confluent-kafka-dotnet` docs:

* [Introduction](https://github.com/edenhill/librdkafka/blob/master/INTRODUCTION.md)
* [Broker version compatibility](https://github.com/edenhill/librdkafka/wiki/Broker-version-compatibility)
* [Using SSL with librdkafka](https://github.com/edenhill/librdkafka/wiki/Using-SSL-with-librdkafka)

### Configuration properties

Producer, consumer and global configuration properties are described [here](https://github.com/edenhill/librdkafka/blob/master/CONFIGURATION.md).

### Minimizing message latency

There is a good description [here](https://github.com/edenhill/librdkafka/wiki/How-to-decrease-message-latency) on improving the latency by applying producer/consumer settings on librdkafka. Here is how you enter the settings using SlimMessageBus:

```cs
var messageBusBuilder = new MessageBusBuilder()
	// ...
	.WithProviderKafka(new KafkaMessageBusSettings(kafkaBrokers)
	{
		ProducerConfigFactory = () => new Dictionary<string, object>
		{
			{"socket.blocking.max.ms",1},
			{"queue.buffering.max.ms",1},
			{"socket.nagle.disable", true}
		},
		ConsumerConfigFactory = (group) => new Dictionary<string, object>
		{
			{"socket.blocking.max.ms", 1},
			{"fetch.error.backoff.ms", 1},
			{"statistics.interval.ms", 500000},
			{"socket.nagle.disable", true}
		}
	});
```
There is also a good discussion around latency in [this issue](https://github.com/confluentinc/confluent-kafka-dotnet/issues/89).

More documentation here:
* [How to decrease message latency](https://github.com/edenhill/librdkafka/wiki/How-to-decrease-message-latency)
* [Reduce latency](https://github.com/confluentinc/confluent-kafka-dotnet/wiki/Producing-messages#reduce-latency)

### Deployment

The `librdkafka` distribution for Windows requires [Visual C++ Redistributable for 2013](https://www.microsoft.com/en-US/download/details.aspx?id=40784) installed on the server. More information can be found [here](https://www.microsoft.com/en-US/download/details.aspx?id=40784).

### Selecting message partition for topic producer

Kafka topics are broken into partitions. The question is how does SMB Kafka choose the partition to assign the message?
There are two possible options:

#### Default partitioner with message key

Currently the [confluent-kafka-dotnet](https://github.com/confluentinc/confluent-kafka-dotnet) does not support custom partitioners (see [here](https://github.com/confluentinc/confluent-kafka-dotnet/issues/343)).
The default partitioner is supported, which works in this way:

* when message key is not provided then partition is assigned using round-robin,
* when message key is provided then same partition is assigned to same key

SMB Kafka allows to set a provider (selector) that will assign the message key for a given message and topic pair. Here is an example:

```cs
IMessageBus messageBus = new MessageBusBuilder()
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

#### Assigning partition explicitly

SMB Kafka allows to set a provider (selector) that will assign the partition number for a given message and topic pair. Here is an example:

```cs
IMessageBus messageBus = new MessageBusBuilder()
	.Produce<PingMessage>(x =>
	{
		x.DefaultTopic("topic1");
		// Partition #0 for even counters
		// Partition #1 for odd counters
		x.PartitionProvider((message, topic) => message.Counter % 2);
	})
	.WithProviderKafka(new KafkaMessageBusSettings(kafkaBrokers))
	.Build();
```

With this approach your provider needs to know the number of partitions for a topic.

### Consumer context

The consumer can implement the `IConsumerContextAware` interface to access the Kafka native message:

```cs
public class PingConsumer : IConsumer<PingMessage>, IConsumerContextAware
{
   public AsyncLocal<ConsumerContext> Context { get; } = new AsyncLocal<ConsumerContext>();

   public Task OnHandle(PingMessage message, string name)
   {
      var messageContext = Context.Value;

      // Kafka transport specific extension:
      var transportMessage = messageContext.GetTransportMessage();
      var partition = transportMessage.TopicPartition.Partition;
   }
}
```

This could be useful to extract the message's offset or partition.
