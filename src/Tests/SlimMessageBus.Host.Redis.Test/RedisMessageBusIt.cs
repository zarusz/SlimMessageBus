using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SecretStore;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.DependencyResolver;
using SlimMessageBus.Host.Serialization.Json;
using Xunit;

namespace SlimMessageBus.Host.Redis.Test
{
    [Trait("Category", "Integration")]
    public class RedisMessageBusIt : IDisposable
    {
        private const int NumberOfMessages = 77;

        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;

        private MessageBusBuilder MessageBusBuilder { get; }
        private Lazy<RedisMessageBus> MessageBus { get; }

        public RedisMessageBusIt()
        {
            _loggerFactory = NullLoggerFactory.Instance;
            _logger = _loggerFactory.CreateLogger<RedisMessageBusIt>();

            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            Secrets.Load(@"..\..\..\..\..\secrets.txt");

            var connectionString = Secrets.Service.PopulateSecrets(configuration["Redis:ConnectionString"]);

            MessageBusBuilder = MessageBusBuilder.Create()
                .WithLoggerFacory(_loggerFactory)
                .WithSerializer(new JsonMessageSerializer())
                .WithProviderRedis(new RedisMessageBusSettings(connectionString));

            MessageBus = new Lazy<RedisMessageBus>(() => (RedisMessageBus)MessageBusBuilder.Build());
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
        public async Task BasicPubSubOnTopic()
        {
            var concurrency = 2;
            var subscribers = 2;
            var topic = "test-ping";

            MessageBusBuilder
                .Produce<PingMessage>(x =>
                {
                    x.DefaultTopic(topic);
                })
                .Do(builder => Enumerable.Range(0, subscribers).ToList().ForEach(i =>
                {
                    builder.Consume<PingMessage>(x => x
                        .Topic(topic)
                        .WithConsumer<PingConsumer>()
                        .Instances(concurrency));
                }));

            await BasicPubSub(concurrency, subscribers).ConfigureAwait(false);
        }

        private async Task BasicPubSub(int concurrency, int subscribers)
        {
            // arrange
            var pingConsumer = new PingConsumer(_loggerFactory.CreateLogger<PingConsumer>());

            MessageBusBuilder
                .WithDependencyResolver(new LookupDependencyResolver(f =>
                {
                    if (f == typeof(PingConsumer)) return pingConsumer;
                    if (f == typeof(ILoggerFactory)) return null;
                    throw new InvalidOperationException();
                }));

            var messageBus = MessageBus.Value;

            // act

            // publish
            var stopwatch = Stopwatch.StartNew();

            var producedMessages = Enumerable
                .Range(0, NumberOfMessages)
                .Select(i => new PingMessage { Counter = i, Value = Guid.NewGuid() })
                .ToList();

            var messageTasks = producedMessages.Select(m => messageBus.Publish(m));
            // wait until all messages are sent
            await Task.WhenAll(messageTasks).ConfigureAwait(false);

            stopwatch.Stop();
            _logger.LogInformation("Published {0} messages in {1}", producedMessages.Count, stopwatch.Elapsed);

            // consume
            stopwatch.Restart();
            var consumersReceivedMessages = await ConsumeAll(pingConsumer, subscribers * producedMessages.Count);
            stopwatch.Stop();

            _logger.LogInformation("Consumed {0} messages in {1}", consumersReceivedMessages.Count, stopwatch.Elapsed);

            // assert

            // ensure number of instances of consumers created matches
            consumersReceivedMessages.Count.Should().Be(subscribers * producedMessages.Count);

            // ensure all messages arrived 
            // ... the count should match
            consumersReceivedMessages.Count.Should().Be(subscribers * producedMessages.Count);
            // ... the content should match
            foreach (var producedMessage in producedMessages)
            {
                var messageCopies = consumersReceivedMessages.Count(x => x.Counter == producedMessage.Counter && x.Value == producedMessage.Value);
                messageCopies.Should().Be(subscribers);
            }
        }

        [Fact]
        public async Task BasicReqRespOnTopic()
        {
            var topic = "test-echo";

            MessageBusBuilder
                .Produce<EchoRequest>(x =>
                {
                    x.DefaultTopic(topic);
                })
                .Handle<EchoRequest, EchoResponse>(x => x.Topic(topic)
                    .WithHandler<EchoRequestHandler>()
                    .Instances(2))
                .ExpectRequestResponses(x =>
                {
                    x.ReplyToTopic("test-echo-resp");
                    x.DefaultTimeout(TimeSpan.FromSeconds(60));
                });

            await BasicReqResp().ConfigureAwait(false);
        }

        private async Task BasicReqResp()
        {
            // arrange
            var consumer = new EchoRequestHandler();

            MessageBusBuilder
                .WithDependencyResolver(new LookupDependencyResolver(f =>
                {
                    if (f == typeof(EchoRequestHandler)) return consumer;
                    throw new InvalidOperationException();
                }));

            var messageBus = MessageBus.Value;

            // act

            // publish
            var stopwatch = Stopwatch.StartNew();

            var requests = Enumerable
                .Range(0, NumberOfMessages)
                .Select(i => new EchoRequest { Index = i, Message = $"Echo {i}" })
                .ToList();

            var responses = new List<Tuple<EchoRequest, EchoResponse>>();
            var responseTasks = requests.Select(async req =>
            {
                var resp = await messageBus.Send(req).ConfigureAwait(false);
                lock (responses)
                {
                    responses.Add(Tuple.Create(req, resp));
                }
            });
            await Task.WhenAll(responseTasks).ConfigureAwait(false);

            stopwatch.Stop();
            _logger.LogInformation("Published and received {0} messages in {1}", responses.Count, stopwatch.Elapsed);

            // assert

            // all messages got back
            responses.Count.Should().Be(NumberOfMessages);
            responses.All(x => x.Item1.Message == x.Item2.Message).Should().BeTrue();
        }

        private static async Task<IList<PingMessage>> ConsumeAll(PingConsumer consumer, int expectedCount)
        {
            var lastMessageCount = 0;
            var lastMessageStopwatch = Stopwatch.StartNew();

            const int newMessagesAwaitingTimeout = 5;

            while (lastMessageStopwatch.Elapsed.TotalSeconds < newMessagesAwaitingTimeout && expectedCount != consumer.Messages.Count)
            {
                await Task.Delay(100).ConfigureAwait(false);

                if (consumer.Messages.Count != lastMessageCount)
                {
                    lastMessageCount = consumer.Messages.Count;
                    lastMessageStopwatch.Restart();
                }
            }
            lastMessageStopwatch.Stop();
            return consumer.Messages;
        }

        private class PingMessage
        {
            public int Counter { get; set; }
            public Guid Value { get; set; }

            #region Overrides of Object

            public override string ToString() => $"PingMessage(Counter={Counter}, Value={Value})";

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

        private class EchoRequest : IRequestMessage<EchoResponse>
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