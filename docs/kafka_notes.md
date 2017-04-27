# SlimMessageBus Kafka Notes

### Underlying client

The SMB Kafka implementation uses [confluent-kafka-dotnet](https://github.com/confluentinc/confluent-kafka-dotnet) .NET wrapper around the native [librdkafka](https://github.com/edenhill/librdkafka) library.

When troubleshooting or fine tuning it is worth reading the `librdkafka` docs:
* [Introduction](https://github.com/edenhill/librdkafka/blob/master/INTRODUCTION.md)
* [How to decrease message latency](https://github.com/edenhill/librdkafka/wiki/How-to-decrease-message-latency)

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

### Deploying

The `librdkafka` distribution for Windows requires [Visual C++ Redistributable for 2013](https://www.microsoft.com/en-US/download/details.aspx?id=40784) installed on the server. More information can be found [here](https://www.microsoft.com/en-US/download/details.aspx?id=40784).
