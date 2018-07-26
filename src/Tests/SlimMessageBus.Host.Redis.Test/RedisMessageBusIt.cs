using Common.Logging;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.Serialization.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SlimMessageBus.Host.Redis.Test
{
    public class RedisMessageBusIt : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const int NumberOfMessages = 77;

        private RedisMessageBusSettings RedisSettings { get; }
        private MessageBusBuilder MessageBusBuilder { get; }
        private Lazy<RedisMessageBus> MessageBus { get; }

        public RedisMessageBusIt()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            var redisServer = configuration["Redis:Server"];
            var redisSyncTimeout = 5000;
            int.TryParse(configuration["Redis:SyncTimeout"], out redisSyncTimeout);

            RedisSettings = new RedisMessageBusSettings(redisServer, redisSyncTimeout);

            MessageBusBuilder = new MessageBusBuilder()
                .WithSerializer(new JsonMessageSerializer())
                .WithProviderRedis(RedisSettings);

            MessageBus = new Lazy<RedisMessageBus>(() => (RedisMessageBus)MessageBusBuilder.Build());
        }

        public void Dispose()
        {
            MessageBus.Value.Dispose();
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void BasicPubSub()
        {
            // arrange

            var topic = $"test-ping";

            var pingConsumer = new PingConsumer();

            MessageBusBuilder
                .Publish<PingMessage>(x =>
                {
                    x.DefaultTopic(topic);
                })
                .SubscribeTo<PingMessage>(x => x.Topic(topic)
                                                .Group(string.Empty) // Note: Group not used in Redis
                                                .WithSubscriber<PingConsumer>()
                                                .Instances(2))
                .WithDependencyResolver(new LookupDependencyResolver(f =>
                {
                    if (f == typeof(PingConsumer)) return pingConsumer;
                    throw new InvalidOperationException();
                }));

            var redisMessageBus = MessageBus.Value;

            // act

            // publish
            var stopwatch = Stopwatch.StartNew();

            var messages = Enumerable
                .Range(0, NumberOfMessages)
                .Select(i => new PingMessage { Counter = i, Timestamp = DateTime.UtcNow })
                .ToList();

            messages
                .AsParallel()
                .ForAll(m => redisMessageBus.Publish(m).Wait());

            stopwatch.Stop();
            Log.InfoFormat("Published {0} messages in {1}", messages.Count, stopwatch.Elapsed);

            // consume
            stopwatch.Restart();
            var messagesReceived = ConsumeFromTopic(pingConsumer);
            stopwatch.Stop();
            Log.InfoFormat("Consumed {0} messages in {1}", messagesReceived.Count, stopwatch.Elapsed);

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
            var topic = $"test-echo";
            var echoRequestHandler = new EchoRequestHandler();

            MessageBusBuilder
                .Publish<EchoRequest>(x =>
                {
                    x.DefaultTopic(topic);
                })
                .Handle<EchoRequest, EchoResponse>(x => x.Topic(topic)
                                                         .Group(string.Empty) // Note: Group not used in Redis
                                                         .WithHandler<EchoRequestHandler>()
                                                         .Instances(2))
                .ExpectRequestResponses(x =>
                {
                    x.ReplyToTopic("test-echo-resp");
                    x.Group(string.Empty); // Note: Group not used in Redis
                    x.DefaultTimeout(TimeSpan.FromSeconds(30));
                })
                .WithDependencyResolver(new LookupDependencyResolver(f =>
                {
                    if (f == typeof(EchoRequestHandler)) return echoRequestHandler;
                    throw new InvalidOperationException();
                }));

            var redisMessageBus = MessageBus.Value;

            // act

            var requests = Enumerable
                .Range(0, NumberOfMessages)
                .Select(i => new EchoRequest { Index = i, Message = $"Echo {i}" })
                .ToList();

            var responses = new List<Tuple<EchoRequest, EchoResponse>>();
            requests.AsParallel().ForAll(req =>
            {
                var resp = redisMessageBus.Send(req).Result;
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

        private IList<PingMessage> ConsumeFromTopic(PingConsumer pingConsumer)
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

        public class PingMessage
        {
            public DateTime Timestamp { get; set; }
            public int Counter { get; set; }
        }

        public class PingConsumer : IConsumer<PingMessage>, IConsumerContextAware
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

                Log.InfoFormat("Got message {0} on topic {1}.", message.Counter, topic);
                return Task.CompletedTask;
            }

            #endregion
        }

        public class EchoRequest : IRequestMessage<EchoResponse>
        {
            public int Index { get; set; }
            public string Message { get; set; }
        }

        public class EchoResponse
        {
            public string Message { get; set; }
        }

        public class EchoRequestHandler : IRequestHandler<EchoRequest, EchoResponse>
        {
            public Task<EchoResponse> OnHandle(EchoRequest request, string topic)
            {
                return Task.FromResult(new EchoResponse { Message = request.Message });
            }
        }
    }
}
