namespace SlimMessageBus.Host.Memory.Test;

using System.Diagnostics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using SlimMessageBus.Host.DependencyResolver;
using SlimMessageBus.Host.MsDependencyInjection;
using SlimMessageBus.Host.Serialization.Json;
using SlimMessageBus.Host.Test.Common;

[Trait("Category", "Integration")]
public class MemoryMessageBusIt : BaseIntegrationTest<MemoryMessageBusIt>
{
    private const int NumberOfMessages = 77;

    private MemoryMessageBusSettings _settings = new();

    public MemoryMessageBusIt(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
    }

    protected override void SetupServices(ServiceCollection services, IConfigurationRoot configuration)
    {
        services.AddSlimMessageBus(mbb =>
        {
            mbb
                .WithSerializer(new JsonMessageSerializer())
                .WithProviderMemory(_settings);

            ApplyBusConfiguration(mbb);
        });
    }

    public IMessageBus MessageBus => ServiceProvider.GetRequiredService<IMessageBus>();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BasicPubSubOnTopic(bool enableSerialization)
    {
        _settings.EnableMessageSerialization = enableSerialization;

        var concurrency = 2;
        var subscribers = 2;
        var topic = "test-ping";

        AddBusConfiguration(mbb =>
        {
            mbb
                .Produce<PingMessage>(x => x.DefaultTopic(topic))
                .Do(builder => Enumerable.Range(0, subscribers).ToList().ForEach(i =>
                {
                    builder.Consume<PingMessage>(x => x
                        .Topic(topic)
                        .WithConsumer<PingConsumer>()
                        .Instances(concurrency));
                }));
        });

        await BasicPubSub(concurrency, subscribers).ConfigureAwait(false);
    }

    private async Task BasicPubSub(int concurrency, int subscribers)
    {
        // arrange
        var pingConsumer = new PingConsumer();

        AddBusConfiguration(mbb =>
        {
            mbb
                .WithDependencyResolver(new LookupDependencyResolver(t =>
                {
                    if (t == typeof(PingConsumer)) return pingConsumer;
                    // for interceptors
                    if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>)) return Enumerable.Empty<object>();
                    if (t == typeof(ILoggerFactory)) return LoggerFactory;
                    throw new InvalidOperationException();
                }));
        });

        var messageBus = MessageBus;

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
        Logger.LogInformation("Published {0} messages in {1}", producedMessages.Count, stopwatch.Elapsed);

        // consume
        stopwatch.Restart();
        var consumersReceivedMessages = await ConsumeAll(pingConsumer, subscribers * producedMessages.Count);
        stopwatch.Stop();

        Logger.LogInformation("Consumed {0} messages in {1}", consumersReceivedMessages.Count, stopwatch.Elapsed);

        // assert

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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BasicReqRespOnTopic(bool enableSerialization)
    {
        _settings.EnableMessageSerialization = enableSerialization;

        var topic = "test-echo";

        AddBusConfiguration(mbb =>
        {
            mbb
                .Produce<EchoRequest>(x => x.DefaultTopic(topic))
                .Handle<EchoRequest, EchoResponse>(x => x.Topic(topic)
                    .WithHandler<EchoRequestHandler>()
                    .Instances(2));
        });

        await BasicReqResp().ConfigureAwait(false);
    }

    private async Task BasicReqResp()
    {
        // arrange
        var consumer = new EchoRequestHandler();

        AddBusConfiguration(mbb =>
        {
            mbb
                .WithDependencyResolver(new LookupDependencyResolver(f =>
                {
                    if (f == typeof(EchoRequestHandler)) return consumer;
                    // for interceptors
                    if (f.IsGenericType && f.GetGenericTypeDefinition() == typeof(IEnumerable<>)) return Enumerable.Empty<object>();
                    if (f == typeof(ILoggerFactory)) return LoggerFactory;
                    throw new InvalidOperationException();
                }));
        });

        var messageBus = MessageBus;

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
        Logger.LogInformation("Published and received {0} messages in {1}", responses.Count, stopwatch.Elapsed);

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

    internal record PingMessage
    {
        public int Counter { get; init; }
        public Guid Value { get; init; }
    }

    internal class PingConsumer : IConsumer<PingMessage>
    {
        public IList<PingMessage> Messages { get; } = new List<PingMessage>();

        public Task OnHandle(PingMessage message)
        {
            lock (this)
            {
                Messages.Add(message);
            }

            Console.WriteLine("Got message {0}.", message.Counter);
            return Task.CompletedTask;
        }
    }

    internal record EchoRequest : IRequestMessage<EchoResponse>
    {
        public int Index { get; init; }
        public string Message { get; init; }
    }

    internal record EchoResponse
    {
        public string Message { get; init; }
    }

    internal class EchoRequestHandler : IRequestHandler<EchoRequest, EchoResponse>
    {
        public Task<EchoResponse> OnHandle(EchoRequest request)
        {
            return Task.FromResult(new EchoResponse { Message = request.Message });
        }
    }
}
