namespace SlimMessageBus.Host.AzureEventHub.Test
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using FluentAssertions;
    using SlimMessageBus.Host.Config;
    using SlimMessageBus.Host.Serialization.Json;
    using Xunit;
    using System.Linq;
    using Microsoft.Extensions.Configuration;
    using SecretStore;
    using SlimMessageBus.Host.DependencyResolver;
    using Microsoft.Extensions.Logging;
    using Xunit.Abstractions;
    using SlimMessageBus.Host.Test.Common;

    [Trait("Category", "Integration")]
    public class EventHubMessageBusIt : IDisposable
    {
        private const int NumberOfMessages = 77;

        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger logger;

        private EventHubMessageBusSettings Settings { get; }
        private MessageBusBuilder MessageBusBuilder { get; }
        private Lazy<EventHubMessageBus> MessageBus { get; }

        public EventHubMessageBusIt(ITestOutputHelper testOutputHelper)
        {
            loggerFactory = new XunitLoggerFactory(testOutputHelper);
            logger = loggerFactory.CreateLogger<EventHubMessageBusIt>();

            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            Secrets.Load(@"..\..\..\..\..\secrets.txt");

            // connection details to the Azure Event Hub
            var connectionString = Secrets.Service.PopulateSecrets(configuration["Azure:EventHub"]);
            var storageConnectionString = Secrets.Service.PopulateSecrets(configuration["Azure:Storage"]);
            var storageContainerName = configuration["Azure:ContainerName"];

            Settings = new EventHubMessageBusSettings(connectionString, storageConnectionString, storageContainerName);

            MessageBusBuilder = MessageBusBuilder.Create()
                .WithLoggerFacory(loggerFactory)
                .WithSerializer(new JsonMessageSerializer())
                .WithProviderEventHub(Settings);

            MessageBus = new Lazy<EventHubMessageBus>(() => (EventHubMessageBus)MessageBusBuilder.Build());
        }

        public void Dispose()
        {
            var stopwatch = Stopwatch.StartNew();
            MessageBus.Value.Dispose();
            stopwatch.Stop();
            logger.LogInformation("Disposed bus in {0}", stopwatch.Elapsed);

            GC.SuppressFinalize(this);
        }


        [Fact]
        public async Task BasicPubSub()
        {
            // arrange
            var hubName = "test-ping";

            var pingConsumer = new PingConsumer(loggerFactory.CreateLogger<PingConsumer>());

            MessageBusBuilder
                .Produce<PingMessage>(x => x.DefaultTopic(hubName).KeyProvider(m => (m.Counter % 2).ToString()))
                .Consume<PingMessage>(x => x.Topic(hubName)
                                                .Group("subscriber") // ensure consumer group exists on the event hub
                                                .WithConsumer<PingConsumer>()
                                                .Instances(2))
                .WithDependencyResolver(new LookupDependencyResolver(f =>
                {
                    if (f == typeof(PingConsumer)) return pingConsumer;
                    throw new InvalidOperationException();
                }));

            var messageBus = MessageBus.Value;

            // act

            // publish
            var stopwatch = Stopwatch.StartNew();

            var messages = Enumerable
                .Range(0, NumberOfMessages)
                .Select(i => new PingMessage { Counter = i, Timestamp = DateTime.UtcNow })
                .ToList();

            foreach (var m in messages)
            {
                await messageBus.Publish(m);
            }

            stopwatch.Stop();
            logger.LogInformation("Published {0} messages in {1}", messages.Count, stopwatch.Elapsed);

            // consume
            stopwatch.Restart();
            var messagesReceived = await ConsumeFromTopic(pingConsumer);
            stopwatch.Stop();
            logger.LogInformation("Consumed {0} messages in {1}", messagesReceived.Count, stopwatch.Elapsed);

            // assert

            // all messages got back
            messagesReceived.Count.Should().Be(messages.Count);
        }

        [Fact]
        public async Task BasicReqResp()
        {
            // arrange

            // ensure the topic has 2 partitions
            var topic = "test-echo";
            var echoRequestHandler = new EchoRequestHandler();

            MessageBusBuilder
                .Produce<EchoRequest>(x =>
                {
                    x.DefaultTopic(topic);
                })
                .Handle<EchoRequest, EchoResponse>(x => x.Topic(topic)
                                                         .Group("handler") // ensure consumer group exists on the event hub
                                                         .WithHandler<EchoRequestHandler>()
                                                         .Instances(2))
                .ExpectRequestResponses(x =>
                {
                    x.ReplyToTopic("test-echo-resp");
                    x.Group("response-reader"); // ensure consumer group exists on the event hub
                    x.DefaultTimeout(TimeSpan.FromSeconds(30));
                })
                .WithDependencyResolver(new LookupDependencyResolver(f =>
                {
                    if (f == typeof(EchoRequestHandler)) return echoRequestHandler;
                    throw new InvalidOperationException();
                }));

            var messageBus = MessageBus.Value;

            // act

            var requests = Enumerable
                .Range(0, NumberOfMessages)
                .Select(i => new EchoRequest { Index = i, Message = $"Echo {i}" })
                .ToList();

            var responses = new List<Tuple<EchoRequest, EchoResponse>>();

            await Task.WhenAll(
                requests.Select(async req =>
                {
                    var resp = await messageBus.Send(req);
                    lock (responses)
                    {
                        responses.Add(Tuple.Create(req, resp));
                    }
                })
            );

            // assert

            // all messages got back
            responses.Count.Should().Be(NumberOfMessages);
            responses.All(x => x.Item1.Message == x.Item2.Message).Should().BeTrue();
        }

        private static async Task<IList<PingMessage>> ConsumeFromTopic(PingConsumer pingConsumer)
        {
            var lastMessageCount = 0;
            var lastMessageStopwatch = Stopwatch.StartNew();

            const int newMessagesAwaitingTimeout = 3;

            while (lastMessageStopwatch.Elapsed.TotalSeconds < newMessagesAwaitingTimeout)
            {
                await Task.Delay(100);

                if (pingConsumer.Messages.Count != lastMessageCount)
                {
                    lastMessageCount = pingConsumer.Messages.Count;
                    lastMessageStopwatch.Restart();
                }
            }
            lastMessageStopwatch.Stop();
            return pingConsumer.Messages;
        }
    }

    public class PingMessage
    {
        public DateTime Timestamp { get; set; }
        public int Counter { get; set; }

        #region Overrides of Object

        public override string ToString() => $"PingMessage(Counter={Counter}, Timestamp={Timestamp})";

        #endregion
    }

    public class PingConsumer : IConsumer<PingMessage>, IConsumerWithContext
    {
        private readonly ILogger logger;

        public PingConsumer(ILogger logger) => this.logger = logger;

        public IConsumerContext Context { get; set; }
        public IList<PingMessage> Messages { get; } = new List<PingMessage>();

        #region Implementation of IConsumer<in PingMessage>

        public Task OnHandle(PingMessage message, string path)
        {
            lock (this)
            {
                Messages.Add(message);
            }

            var msg = Context.GetTransportMessage();

            logger.LogInformation("Got message {0:000} on topic {1} offset {2} partition key {3}.", message.Counter, path, msg.SystemProperties.Offset, msg.SystemProperties.PartitionKey);
            return Task.CompletedTask;
        }

        #endregion
    }

    public class EchoRequest : IRequestMessage<EchoResponse>
    {
        public int Index { get; set; }
        public string Message { get; set; }

        #region Overrides of Object

        public override string ToString() => $"EchoRequest(Index={Index}, Message={Message})";

        #endregion
    }

    public class EchoResponse
    {
        public string Message { get; set; }

        #region Overrides of Object

        public override string ToString() => $"EchoResponse(Message={Message})";

        #endregion
    }

    public class EchoRequestHandler : IRequestHandler<EchoRequest, EchoResponse>
    {
        public Task<EchoResponse> OnHandle(EchoRequest request, string path)
        {
            return Task.FromResult(new EchoResponse { Message = request.Message });
        }
    }

}