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
public class KafkaMessageBusIt : BaseIntegrationTest<KafkaMessageBusIt>
{
    private const int NumberOfMessages = 77;
    private string TopicPrefix { get; set; }

    private static void AddSsl(string username, string password, ClientConfig c)
    {
        // cloudkarafka.com uses SSL with SASL authentication
        c.SecurityProtocol = SecurityProtocol.SaslSsl;
        c.SaslUsername = username;
        c.SaslPassword = password;
        c.SaslMechanism = SaslMechanism.ScramSha256;
        c.SslCaLocation = "cloudkarafka_2023-10.pem";
    }

    public KafkaMessageBusIt(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
    }

    protected override void SetupServices(ServiceCollection services, IConfigurationRoot configuration)
    {
        var kafkaBrokers = Secrets.Service.PopulateSecrets(configuration["Kafka:Brokers"]);
        var kafkaUsername = Secrets.Service.PopulateSecrets(configuration["Kafka:Username"]);
        var kafkaPassword = Secrets.Service.PopulateSecrets(configuration["Kafka:Password"]);
        var kafkaSecure = Convert.ToBoolean(Secrets.Service.PopulateSecrets(configuration["Kafka:Secure"]));

        // Topics on cloudkarafka.com are prefixed with username
        TopicPrefix = $"{kafkaUsername}-";

        services
            .AddSlimMessageBus((mbb) =>
            {
                mbb.WithProviderKafka(cfg =>
                {
                    cfg.BrokerList = kafkaBrokers;
                    cfg.ProducerConfig = (config) =>
                    {
                        if (kafkaSecure)
                        {
                            AddSsl(kafkaUsername, kafkaPassword, config);
                        }

                        config.LingerMs = 5; // 5ms
                        config.SocketNagleDisable = true;
                    };
                    cfg.ConsumerConfig = (config) =>
                    {
                        if (kafkaSecure)
                        {
                            AddSsl(kafkaUsername, kafkaPassword, config);
                        }

                        config.FetchErrorBackoffMs = 1;
                        config.SocketNagleDisable = true;

                        config.StatisticsIntervalMs = 500000;
                        config.AutoOffsetReset = AutoOffsetReset.Latest;
                    };
                });
                mbb.AddServicesFromAssemblyContaining<PingConsumer>();
                mbb.AddJsonSerializer();

                ApplyBusConfiguration(mbb);
            });

        services.AddSingleton<TestEventCollector<ConsumedMessage>>();
    }

    public IMessageBus MessageBus => ServiceProvider.GetRequiredService<IMessageBus>();

    [Fact]
    public async Task BasicPubSub()
    {
        // arrange
        AddBusConfiguration(mbb =>
        {
            var topic = $"{TopicPrefix}test-ping";
            mbb
                .Produce<PingMessage>(x =>
                {
                    x.DefaultTopic(topic);
                    // Partition #0 for even counters
                    // Partition #1 for odd counters
                    x.PartitionProvider((m, t) => m.Counter % 2);
                })
                .Consume<PingMessage>(x =>
                {
                    x.Topic(topic)
                        .WithConsumer<PingConsumer>()
                        .KafkaGroup("subscriber")
                        .Instances(2)
                        .CheckpointEvery(1000)
                        .CheckpointAfter(TimeSpan.FromSeconds(600));
                });
        });

        var consumedMessages = ServiceProvider.GetRequiredService<TestEventCollector<ConsumedMessage>>();
        var messageBus = MessageBus;

        // act

        // consume all messages that might be on the queue/subscription
        await consumedMessages.WaitUntilArriving(newMessagesTimeout: 5);
        consumedMessages.Clear();

        // publish
        var stopwatch = Stopwatch.StartNew();

        var messages = Enumerable
            .Range(0, NumberOfMessages)
            .Select(i => new PingMessage { Counter = i, Timestamp = DateTime.UtcNow })
            .ToList();

        await Task.WhenAll(messages.Select(m => messageBus.Publish(m)));

        stopwatch.Stop();
        Logger.LogInformation("Published {0} messages in {1}", messages.Count, stopwatch.Elapsed);

        // consume
        stopwatch.Restart();

        await consumedMessages.WaitUntilArriving(newMessagesTimeout: 5);

        stopwatch.Stop();
        Logger.LogInformation("Consumed {0} messages in {1}", consumedMessages.Count, stopwatch.Elapsed);

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
            var topic = $"{TopicPrefix}test-echo";
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
                                                         .CheckpointEvery(1000)
                                                         .CheckpointAfter(TimeSpan.FromSeconds(60)))
                .ExpectRequestResponses(x =>
                {
                    x.ReplyToTopic($"{TopicPrefix}test-echo-resp");
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
            .Select(i => new EchoRequest { Index = i, Message = $"Echo {i}" })
            .ToList();

        var responses = new ConcurrentBag<ValueTuple<EchoRequest, EchoResponse>>();
        await Task.WhenAll(requests.Select(async req =>
        {
            var resp = await kafkaMessageBus.Send<EchoResponse, EchoRequest>(req);
            responses.Add((req, resp));
        }));

        await responses.WaitUntilArriving(newMessagesTimeout: 10);

        // assert

        // all messages got back
        responses.Count.Should().Be(NumberOfMessages);
        responses.All(x => x.Item1.Message == x.Item2.Message).Should().BeTrue();
    }

    private class PingMessage
    {
        public DateTime Timestamp { get; set; }
        public int Counter { get; set; }

        #region Overrides of Object

        public override string ToString() => $"PingMessage(Counter={Counter}, Timestamp={Timestamp})";

        #endregion
    }

    record struct ConsumedMessage(PingMessage Message, int Partition);

    private class PingConsumer(ILogger<PingConsumer> logger, TestEventCollector<ConsumedMessage> messages)
        : IConsumer<PingMessage>, IConsumerWithContext
    {
        private readonly ILogger _logger = logger;
        private readonly TestEventCollector<ConsumedMessage> _messages = messages;

        public IConsumerContext Context { get; set; }

        #region Implementation of IConsumer<in PingMessage>

        public Task OnHandle(PingMessage message)
        {
            var transportMessage = Context.GetTransportMessage();
            var partition = transportMessage.TopicPartition.Partition;

            _messages.Add(new ConsumedMessage(message, partition));

            _logger.LogInformation("Got message {0:000} on topic {1}.", message.Counter, Context.Path);
            return Task.CompletedTask;
        }

        #endregion
    }

    private class EchoRequest /*: IRequest<EchoResponse>*/
    {
        public int Index { get; set; }
        public string Message { get; set; }

        #region Overrides of Object

        public override string ToString() => $"EchoRequest(Index={Index}, Message={Message})";

        #endregion
    }

    private class EchoResponse
    {
        public string Message { get; set; }

        #region Overrides of Object

        public override string ToString() => $"EchoResponse(Message={Message})";

        #endregion
    }

    private class EchoRequestHandler : IRequestHandler<EchoRequest, EchoResponse>
    {
        public Task<EchoResponse> OnHandle(EchoRequest request)
        {
            return Task.FromResult(new EchoResponse { Message = request.Message });
        }
    }
}