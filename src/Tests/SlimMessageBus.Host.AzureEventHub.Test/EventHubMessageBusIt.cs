using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using FluentAssertions;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.Serialization.Json;
using Xunit;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using SlimMessageBus.Host.DependencyResolver;

namespace SlimMessageBus.Host.AzureEventHub.Test
{
    public class EventHubMessageBusIt : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const int NumberOfMessages = 77;

        private EventHubMessageBusSettings Settings { get; }
        private MessageBusBuilder MessageBusBuilder { get; }
        private Lazy<EventHubMessageBus> MessageBus { get; }

        public EventHubMessageBusIt()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            // address to the Kafka broker
            var connectionString = configuration["Azure:EventHub"];
            var storageConnectionString = configuration["Azure:Storage"];
            var storageContainerName = configuration["Azure:ContainerName"];

            Settings = new EventHubMessageBusSettings(connectionString, storageConnectionString, storageContainerName);

            MessageBusBuilder = MessageBusBuilder.Create()
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
        [Trait("Category", "Integration")]
        public void BasicPubSub()
        {
            // arrange
            var topic = "test-ping";

            var pingConsumer = new PingConsumer();

            MessageBusBuilder
                .Produce<PingMessage>(x =>
                {
                    x.DefaultTopic(topic);
                })
                .SubscribeTo<PingMessage>(x => x.Topic(topic)
                                                .Group("subscriber") // ensure consumer group exists on the event hub
                                                .WithSubscriber<PingConsumer>()
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
            Log.InfoFormat(CultureInfo.InvariantCulture, "Published {0} messages in {1}", messages.Count, stopwatch.Elapsed);

            // consume
            stopwatch.Restart();
            var messagesReceived = ConsumeFromTopic(pingConsumer);
            stopwatch.Stop();
            Log.InfoFormat(CultureInfo.InvariantCulture, "Consumed {0} messages in {1}", messagesReceived.Count, stopwatch.Elapsed);

            // assert

            // all messages got back
            messagesReceived.Count.Should().Be(messages.Count);           
        }

        [Fact]
        [Trait("Category", "Integration")]
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

            var kafkaMessageBus = MessageBus.Value;

            // act

            var requests = Enumerable
                .Range(0, NumberOfMessages)
                .Select(i => new EchoRequest { Index = i, Message = $"Echo {i}" })
                .ToList();

            var responses = new List<Tuple<EchoRequest, EchoResponse>>();
            requests.AsParallel().ForAll(req =>
            {
                var resp = kafkaMessageBus.Send(req).Result;
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

            const int newMessagesAwatingTimeout = 10;

            while (lastMessageStopwatch.Elapsed.TotalSeconds < newMessagesAwatingTimeout)
            {
                Thread.Sleep(200);

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
        }

        private class PingConsumer : IConsumer<PingMessage>, IConsumerContextAware
        {
            private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

            public AsyncLocal<ConsumerContext> Context { get; } = new AsyncLocal<ConsumerContext>();
            public IList<PingMessage> Messages { get; } = new List<PingMessage>();

            #region Implementation of IConsumer<in PingMessage>

            public Task OnHandle(PingMessage message, string topic)
            {
                lock (this)
                {
                    Messages.Add(message);
                }

                Log.InfoFormat(CultureInfo.InvariantCulture, "Got message {0} on topic {1}.", message.Counter, topic);
                return Task.CompletedTask;
            }

            #endregion
        }

        private class EchoRequest: IRequestMessage<EchoResponse>
        {
            public int Index { get; set; }
            public string Message { get; set; }
        }

        private class EchoResponse
        {
            public string Message { get; set; }
        }

        private class EchoRequestHandler : IRequestHandler<EchoRequest, EchoResponse>
        {
            public Task<EchoResponse> OnHandle(EchoRequest request, string topic)
            {
                return Task.FromResult(new EchoResponse { Message = request.Message });
            }
        }
    }
}