using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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

namespace SlimMessageBus.Host.AzureServiceBus.Test
{
    [Trait("Category", "Integration")]
    public class ServiceBusMessageBusIt : IDisposable
    {
        private const int NumberOfMessages = 77;
        
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;

        private ServiceBusMessageBusSettings Settings { get; }
        private MessageBusBuilder MessageBusBuilder { get; }
        private Lazy<ServiceBusMessageBus> MessageBus { get; }

        public ServiceBusMessageBusIt()
        {
            _loggerFactory = NullLoggerFactory.Instance;
            _logger = _loggerFactory.CreateLogger<ServiceBusMessageBusIt>();

            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            Secrets.Load(@"..\..\..\..\..\secrets.txt");

            var connectionString = Secrets.Service.PopulateSecrets(configuration["Azure:ServiceBus"]);

            Settings = new ServiceBusMessageBusSettings(connectionString);

            MessageBusBuilder = MessageBusBuilder.Create()
                .WithLoggerFacory(_loggerFactory)
                .WithSerializer(new JsonMessageSerializer())
                .WithProviderServiceBus(Settings);

            MessageBus = new Lazy<ServiceBusMessageBus>(() => (ServiceBusMessageBus)MessageBusBuilder.Build());
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
                    // this is optional
                    x.WithModifier((message, sbMessage) =>
                    {
                        // set the Azure SB message ID
                        sbMessage.MessageId = $"ID_{message.Counter}";
                        // set the Azure SB message partition key
                        sbMessage.PartitionKey = message.Counter.ToString(CultureInfo.InvariantCulture);
                    });
                })
                .Do(builder => Enumerable.Range(0, subscribers).ToList().ForEach(i =>
                {
                    builder.Consume<PingMessage>(x => x
                        .Topic(topic)
                        .SubscriptionName($"subscriber-{i}") // ensure subscription exists on the ServiceBus topic
                        .WithConsumer<PingConsumer>()
                        .Instances(concurrency));
                }));

            await BasicPubSub(concurrency, subscribers, subscribers).ConfigureAwait(false);
        }

        [Fact]
        public async Task BasicPubSubOnQueue()
        {
            var concurrency = 2;
            var subscribers = 2;
            var queue = "test-ping-queue";

            MessageBusBuilder
                .Produce<PingMessage>(x =>
                {
                    x.DefaultQueue(queue);
                    // this is optional
                    x.WithModifier((message, sbMessage) =>
                    {
                        // set the Azure SB message ID
                        sbMessage.MessageId = GetMessageId(message);
                        // set the Azure SB message partition key
                        sbMessage.PartitionKey = message.Counter.ToString(CultureInfo.InvariantCulture);
                    });
                })
                .Do(builder => Enumerable.Range(0, subscribers).ToList().ForEach(i =>
                {
                    builder.Consume<PingMessage>(x => x
                        .Queue(queue)
                        .WithConsumer<PingConsumer>()
                        .Instances(concurrency));
                }));

            await BasicPubSub(concurrency, subscribers, 1).ConfigureAwait(false);
        }

        private string GetMessageId(PingMessage message) => $"ID_{message.Counter}";

        private async Task BasicPubSub(int concurrency, int subscribers, int expectedMessageCopies)
        {
            // arrange
            var consumersCreated = new ConcurrentBag<PingConsumer>();

            MessageBusBuilder
                .WithDependencyResolver(new LookupDependencyResolver(f =>
                {
                    if (f != typeof(PingConsumer)) throw new InvalidOperationException();
                    var pingConsumer = new PingConsumer(_loggerFactory.CreateLogger<PingConsumer>());
                    consumersCreated.Add(pingConsumer);
                    return pingConsumer;
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
            var pingConsumerConsumptionTasks = consumersCreated.Select(ConsumeAll);
            var consumersReceivedMessages = await Task.WhenAll(pingConsumerConsumptionTasks).ConfigureAwait(false);
            stopwatch.Stop();

            foreach (var receivedMessages in consumersReceivedMessages)
            {
                _logger.LogInformation("Consumed {0} messages in {1}", receivedMessages.Count, stopwatch.Elapsed);
            }

            // assert

            // ensure number of instances of consumers created matches
            consumersCreated.Count.Should().Be(subscribers * concurrency);
            consumersReceivedMessages.Length.Should().Be(subscribers * concurrency);

            // ensure all messages arrived 
            var totalReceivedMessages = consumersReceivedMessages.SelectMany(x => x).ToList();
            // ... the count should match
            totalReceivedMessages.Count.Should().Be(expectedMessageCopies * producedMessages.Count);
            // ... the content should match
            foreach (var producedMessage in producedMessages)
            {
                var messageCopies = totalReceivedMessages.Count(x => x.Message.Counter == producedMessage.Counter && x.Message.Value == producedMessage.Value && x.MessageId == GetMessageId(x.Message));
                messageCopies.Should().Be(expectedMessageCopies);
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
                    // this is optional
                    x.WithModifier((message, sbMessage) =>
                    {
                        // set the Azure SB message ID
                        sbMessage.MessageId = $"ID_{message.Index}";
                        // set the Azure SB message partition key
                        sbMessage.PartitionKey = message.Index.ToString(CultureInfo.InvariantCulture);
                    });
                })
                .Handle<EchoRequest, EchoResponse>(x => x.Topic(topic)
                    .SubscriptionName("handler")
                    .WithHandler<EchoRequestHandler>()
                    .Instances(2))
                .ExpectRequestResponses(x =>
                {
                    x.ReplyToTopic("test-echo-resp");
                    x.SubscriptionName("response-consumer");
                    x.DefaultTimeout(TimeSpan.FromSeconds(60));
                });

            await BasicReqResp().ConfigureAwait(false);
        }

        [Fact]
        public async Task BasicReqRespOnQueue()
        {
            var queue = "test-echo-queue";

            MessageBusBuilder
                .Produce<EchoRequest>(x =>
                {
                    x.DefaultQueue(queue);
                })
                .Handle<EchoRequest, EchoResponse>(x => x.Queue(queue)
                    .WithHandler<EchoRequestHandler>()
                    .Instances(2))
                .ExpectRequestResponses(x =>
                {
                    x.ReplyToQueue("test-echo-queue-resp");
                    x.DefaultTimeout(TimeSpan.FromSeconds(60));
                });

            await BasicReqResp().ConfigureAwait(false);
        }

        private async Task BasicReqResp()
        {
            // arrange
            var consumersCreated = new ConcurrentBag<EchoRequestHandler>();

            MessageBusBuilder
                .WithDependencyResolver(new LookupDependencyResolver(f =>
                {
                    if (f != typeof(EchoRequestHandler)) throw new InvalidOperationException();
                    var consumer = new EchoRequestHandler();
                    consumersCreated.Add(consumer);
                    return consumer;
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

        private static async Task<IList<(PingMessage Message, string MessageId)>> ConsumeAll(PingConsumer consumer)
        {
            var lastMessageCount = 0;
            var lastMessageStopwatch = Stopwatch.StartNew();

            const int newMessagesAwaitingTimeout = 10;

            while (lastMessageStopwatch.Elapsed.TotalSeconds < newMessagesAwaitingTimeout)
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

            public IList<(PingMessage, string)> Messages { get; } = new List<(PingMessage, string)>();           

            #region Implementation of IConsumer<in PingMessage>

            public Task OnHandle(PingMessage message, string name)
            {
                lock (this)
                {
                    var sbMessage = Context.Value.GetTransportMessage();

                    Messages.Add((message, sbMessage.MessageId));
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