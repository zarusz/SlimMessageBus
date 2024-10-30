namespace SlimMessageBus.Host.Kafka.Test;

using System.Collections.Concurrent;
using System.Diagnostics;

using Confluent.Kafka;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using SlimMessageBus.Host;
using SlimMessageBus.Host.Serialization.Json;
using SlimMessageBus.Host.Test.Common.IntegrationTest;

/// <summary>
/// Performs basic integration test to verify that pub/sub and request-response communication works while concurrent producers pump data.
/// <remarks>
/// Ensure the topics used in this test (test-ping and test-echo) have 2 partitions, otherwise you will get an exception (Confluent.Kafka.KafkaException : Local: Unknown partition)
/// See https://kafka.apache.org/quickstart#quickstart_createtopic
/// <code>bin\windows\kafka-topics.bat --create --zookeeper localhost:2181 --partitions 2 --replication-factor 1 --topic test-ping</code>
/// <code>bin\windows\kafka-topics.bat --create --zookeeper localhost:2181 --partitions 2 --replication-factor 1 --topic test-echo</code>
/// </remarks>
/// </summary>
[Trait("Category", "Integration")]
[Trait("Transport", "Kafka")]
public class KafkaMessageBusIt(ITestOutputHelper testOutputHelper)
    : BaseIntegrationTest<KafkaMessageBusIt>(testOutputHelper)
{
    private const int NumberOfMessages = 300;
    private readonly static TimeSpan DelayTimeSpan = TimeSpan.FromSeconds(5);

    protected override void SetupServices(ServiceCollection services, IConfigurationRoot configuration)
    {
        var kafkaBrokers = Secrets.Service.PopulateSecrets(configuration["Kafka:Brokers"]);

        services
            .AddSlimMessageBus((mbb) =>
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
                        // when the test containers start there is no consumer group yet, so we want to start from the beginning
                        config.AutoOffsetReset = AutoOffsetReset.Earliest;
                    };
                });
                mbb.AddServicesFromAssemblyContaining<PingConsumer>();
                mbb.AddJsonSerializer();

                ApplyBusConfiguration(mbb);
            });

        services.AddSingleton<TestEventCollector<ConsumedMessage>>();
    }

    public IMessageBus MessageBus => ServiceProvider.GetRequiredService<IMessageBus>();

    [Theory]
    [InlineData(300, 100, 120, true)]
    [InlineData(300, 120, 100, true)]
    [InlineData(300, 100, 120, false)]
    [InlineData(300, 120, 100, false)]
    public async Task BasicPubSub(int numberOfMessages, int delayProducerAt, int delayConsumerAt, bool enableProduceAwait)
    {
        // arrange
        AddBusConfiguration(mbb =>
        {
            var topic = "test-ping";
            // doc:fragment:ExampleEnableProduceAwait
            mbb.Produce<PingMessage>(x =>
            {
                x.DefaultTopic(topic);
                // Partition #0 - for even counters, and #1 - for odd counters
                x.PartitionProvider((m, t) => m.Counter % 2);
                x.EnableProduceAwait(enableProduceAwait);
            });
            // doc:fragment:ExampleEnableProduceAwait
            // doc:fragment:ExampleCheckpointConfig
            mbb.Consume<PingMessage>(x =>
            {
                x.Topic(topic)
                    .WithConsumer<PingConsumer>()
                    .KafkaGroup("subscriber")
                    .Instances(2)
                    .CheckpointEvery(1000)
                    .CheckpointAfter(TimeSpan.FromSeconds(600));
            });
            // doc:fragment:ExampleCheckpointConfig

            // or
            //mbb.EnableProduceAwait(enableProduceAwait);
        });

        var consumedMessages = ServiceProvider.GetRequiredService<TestEventCollector<ConsumedMessage>>();
        var consumerControl = ServiceProvider.GetRequiredService<IConsumerControl>();
        var messageBus = MessageBus;

        // act

        // consume all messages that might be on the queue/subscription
        await consumedMessages.WaitUntilArriving(newMessagesTimeout: 5);
        consumedMessages.Clear();

        var pauseAtOffsets = new HashSet<int> { delayConsumerAt };

        consumedMessages.OnAdded += (IList<ConsumedMessage> messages, ConsumedMessage message) =>
        {
            // At the given index message stop the consumers, to simulate a pause in the processing to check if it resumes exactly from the next message
            if (pauseAtOffsets.Contains(message.Message.Counter))
            {
                // Remove self to cause only delay once (in case the same message gets repeated)
                pauseAtOffsets.Remove(message.Message.Counter);

                consumerControl.Stop().ContinueWith(async (_) =>
                {
                    await Task.Delay(DelayTimeSpan);
                    await consumerControl.Start();
                });
            }
        };

        // publish
        var stopwatch = Stopwatch.StartNew();

        var messages = Enumerable
            .Range(0, numberOfMessages)
            .Select(i => new PingMessage(DateTime.UtcNow, i))
            .ToList();

        var index = 0;
        foreach (var m in messages)
        {
            if (index == delayProducerAt)
            {
                // We want to force the Partition EOF event to be triggered by Kafka
                Logger.LogInformation("Waiting some time before publish to force Partition EOF event (MessageIndex: {MessageIndex})", index);
                await Task.Delay(DelayTimeSpan);
            }
            await messageBus.Publish(m);
            index++;
        }

        stopwatch.Stop();
        Logger.LogInformation("Published {MessageCount} messages in {ProduceTime} including simulated delay {DelayTime}", messages.Count, stopwatch.Elapsed, DelayTimeSpan);

        // consume
        stopwatch.Restart();

        await consumedMessages.WaitUntilArriving(newMessagesTimeout: 10);

        stopwatch.Stop();
        Logger.LogInformation("Consumed {MessageCount} messages in {ConsumeTime} including simlulated delay {DelayTime}", consumedMessages.Count, stopwatch.Elapsed, DelayTimeSpan);

        // assert

        // all messages got back
        consumedMessages.Count.Should().Be(messages.Count);

        // Partition #0 => Messages with even counter
        consumedMessages.Snapshot()
            .Where(x => x.Partition == 0)
            .All(x => x.Message.Counter % 2 == 0)
            .Should().BeTrue();

        // Partition #1 => Messages with odd counter
        consumedMessages.Snapshot()
            .Where(x => x.Partition == 1)
            .All(x => x.Message.Counter % 2 == 1)
            .Should().BeTrue();
    }

    [Fact]
    public async Task BasicReqResp()
    {
        // arrange

        // ensure the topic has 2 partitions

        AddBusConfiguration(mbb =>
        {
            var topic = "test-echo";
            mbb
                .Produce<EchoRequest>(x =>
                {
                    x.DefaultTopic(topic);
                    // Partition #0 for even indices
                    // Partition #1 for odd indices
                    x.PartitionProvider((m, t) => m.Index % 2);
                })
                .Handle<EchoRequest, EchoResponse>(x => x.Topic(topic)
                                                         .WithHandler<EchoRequestHandler>()
                                                         .KafkaGroup("handler")
                                                         .Instances(2)
                                                         .CheckpointEvery(100)
                                                         .CheckpointAfter(TimeSpan.FromSeconds(10)))
                .ExpectRequestResponses(x =>
                {
                    x.ReplyToTopic("test-echo-resp");
                    x.KafkaGroup("response-reader");
                    // for subsequent test runs allow enough time for kafka to reassign the partitions
                    x.DefaultTimeout(TimeSpan.FromSeconds(60));
                    x.CheckpointEvery(100);
                    x.CheckpointAfter(TimeSpan.FromSeconds(10));
                });
        });

        var kafkaMessageBus = MessageBus;

        // act

        var requests = Enumerable
            .Range(0, NumberOfMessages)
            .Select(i => new EchoRequest(i, $"Echo {i}"))
            .ToList();

        var responses = new ConcurrentBag<ValueTuple<EchoRequest, EchoResponse>>();
        await Task.WhenAll(requests.Select(async req =>
        {
            try
            {
                var resp = await kafkaMessageBus.Send<EchoResponse, EchoRequest>(req);
                responses.Add((req, resp));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to send request {RequestIndex:000}", req.Index);
            }
        }));

        await responses.WaitUntilArriving(newMessagesTimeout: 5);

        // assert

        // all messages got back
        responses.Count.Should().Be(NumberOfMessages);
        responses.All(x => x.Item1.Message == x.Item2.Message).Should().BeTrue();
    }

    private record PingMessage(DateTime Timestamp, int Counter);

    record struct ConsumedMessage(PingMessage Message, int Partition);

    private class PingConsumer(ILogger<PingConsumer> logger, TestEventCollector<ConsumedMessage> messages)
        : IConsumer<PingMessage>, IConsumerWithContext
    {
        public IConsumerContext Context { get; set; }

        public Task OnHandle(PingMessage message)
        {
            var transportMessage = Context.GetTransportMessage();
            var partition = transportMessage.TopicPartition.Partition;

            messages.Add(new ConsumedMessage(message, partition));

            logger.LogInformation("Got message {MessageCounter:000} on topic {TopicName}.", message.Counter, Context.Path);
            return Task.CompletedTask;
        }
    }

    private record EchoRequest(int Index, string Message);

    private record EchoResponse(string Message);

    private class EchoRequestHandler : IRequestHandler<EchoRequest, EchoResponse>
    {
        public Task<EchoResponse> OnHandle(EchoRequest request)
            => Task.FromResult(new EchoResponse(request.Message));
    }
}