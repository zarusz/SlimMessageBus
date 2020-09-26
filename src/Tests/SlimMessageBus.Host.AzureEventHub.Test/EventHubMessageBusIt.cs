using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
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
using Microsoft.Extensions.Logging.Abstractions;

namespace SlimMessageBus.Host.AzureEventHub.Test
{
    [Trait("Category", "Integration")]
    public class EventHubMessageBusIt : IDisposable
    {
        private const int NumberOfMessages = 77;

        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;

        private EventHubMessageBusSettings Settings { get; }
        private MessageBusBuilder MessageBusBuilder { get; }
        private Lazy<EventHubMessageBus> MessageBus { get; }

        public EventHubMessageBusIt()
        {
            _loggerFactory = NullLoggerFactory.Instance;
            _logger = _loggerFactory.CreateLogger<EventHubMessageBusIt>();

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
                .WithLoggerFacory(_loggerFactory)
                .WithSerializer(new JsonMessageSerializer())
                .WithProviderEventHub(Settings);

            MessageBus = new Lazy<EventHubMessageBus>(() => (EventHubMessageBus) MessageBusBuilder.Build());
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                MessageBus.Value.Dispose();
            }
        }

        [Fact]
        public void BasicPubSub()
        {
            // arrange
            var topic = "test-ping";

            var pingConsumer = new PingConsumer(_loggerFactory.CreateLogger<PingConsumer>());

            MessageBusBuilder
                .Produce<PingMessage>(x => x.DefaultTopic(topic))
                .Consume<PingMessage>(x => x.Topic(topic)
                                                .Group("subscriber") // ensure consumer group exists on the event hub
                                                .WithConsumer<PingConsumer>()
                                                .Instances(2))
                .WithDependencyResolver(new LookupDependencyResolver(f =>
                {
                    if (f == typeof(PingConsumer)) return pingConsumer;
                    throw new InvalidOperationException();
                }));                                                

            var kafkaMessageBus = MessageBus.Value;

            // act

            // publish
            var stopwatch = Stopwatch.StartNew();

            var messages = Enumerable
                .Range(0, NumberOfMessages)
                .Select(i => new PingMessage { Counter = i, Timestamp = DateTime.UtcNow })
                .ToList();

            messages
                .AsParallel()
                .ForAll(m => kafkaMessageBus.Publish(m).Wait());

            stopwatch.Stop();
            _logger.LogInformation("Published {0} messages in {1}", messages.Count, stopwatch.Elapsed);

            // consume
            stopwatch.Restart();
            var messagesReceived = ConsumeFromTopic(pingConsumer);
            stopwatch.Stop();
            _logger.LogInformation("Consumed {0} messages in {1}", messagesReceived.Count, stopwatch.Elapsed);

            // assert

            // all messages got back
            messagesReceived.Count.Should().Be(messages.Count);           
        }

        [Fact]
        public void BasicReqResp()
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
            requests.AsParallel().ForAll(req =>
            {
                var resp = messageBus.Send(req).Result;
                lock (responses)
                {
                    responses.Add(Tuple.Create(req, resp));
                }
            });

            // assert

            // all messages got back
            responses.Count.Should().Be(NumberOfMessages);
            responses.All(x => x.Item1.Message == x.Item2.Message).Should().BeTrue();
        }

        private static IList<PingMessage> ConsumeFromTopic(PingConsumer pingConsumer)
        {
            var lastMessageCount = 0;
            var lastMessageStopwatch = Stopwatch.StartNew();

            const int newMessagesAwaitingTimeout = 5;

            while (lastMessageStopwatch.Elapsed.TotalSeconds < newMessagesAwaitingTimeout)
            {
                Thread.Sleep(100);

                if (pingConsumer.Messages.Count != lastMessageCount)
                {
                    lastMessageCount = pingConsumer.Messages.Count;
                    lastMessageStopwatch.Restart();
                }
            }
            lastMessageStopwatch.Stop();
            return pingConsumer.Messages;
        }

        private class PingMessage
        {
            public DateTime Timestamp { get; set; }
            public int Counter { get; set; }

            #region Overrides of Object

            public override string ToString() => $"PingMessage(Counter={Counter}, Timestamp={Timestamp})";

            #endregion
        }

        private class PingConsumer : IConsumer<PingMessage>, IConsumerContextAware
        {
            private readonly ILogger _logger;

            public PingConsumer(ILogger logger)
            {
                _logger = logger;
            }

            public AsyncLocal<ConsumerContext> Context { get; } = new AsyncLocal<ConsumerContext>();
            public IList<PingMessage> Messages { get; } = new List<PingMessage>();

            #region Implementation of IConsumer<in PingMessage>

            public Task OnHandle(PingMessage message, string name)
            {
                lock (this)
                {
                    Messages.Add(message);
                }

                _logger.LogInformation("Got message {0} on topic {1}.", message.Counter, name);
                return Task.CompletedTask;
            }

            #endregion
        }

        private class EchoRequest: IRequestMessage<EchoResponse>
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
            public Task<EchoResponse> OnHandle(EchoRequest request, string name)
            {
                return Task.FromResult(new EchoResponse { Message = request.Message });
            }
        }
    }
}