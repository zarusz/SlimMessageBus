namespace SlimMessageBus.Host.Memory.Test;

using System.Collections.Concurrent;
using System.Diagnostics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using SlimMessageBus.Host;
using SlimMessageBus.Host.Serialization.Json;
using SlimMessageBus.Host.Test.Common.IntegrationTest;

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
        }, addConsumersFromAssembly: new[] { typeof(PingConsumer).Assembly });

        services.AddSingleton<TestEventCollector<PingMessage>>();
    }

    public IMessageBus MessageBus => ServiceProvider.GetRequiredService<IMessageBus>();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BasicPubSubOnTopic(bool enableSerialization)
    {
        _settings.EnableMessageSerialization = enableSerialization;

        var subscribers = 2;
        var topic = "test-ping";

        AddBusConfiguration(mbb =>
        {
            mbb
                .Produce<PingMessage>(x => x.DefaultTopic(topic))
                .Do(builder => Enumerable.Range(0, subscribers).ToList().ForEach(i =>
                {
                    builder.Consume<PingMessage>(x => x.Topic(topic));
                }));
        });

        await BasicPubSub(subscribers).ConfigureAwait(false);
    }

    private async Task BasicPubSub(int subscribers)
    {
        // arrange
        var messageBus = MessageBus;
        var consumedMessages = ServiceProvider.GetRequiredService<TestEventCollector<PingMessage>>();

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
        await consumedMessages.WaitUntilArriving(expectedCount: subscribers * producedMessages.Count);
        stopwatch.Stop();

        Logger.LogInformation("Consumed {0} messages in {1}", consumedMessages.Count, stopwatch.Elapsed);

        // assert

        // ensure all messages arrived 
        // ... the count should match
        consumedMessages.Count.Should().Be(subscribers * producedMessages.Count);
        // ... the content should match
        foreach (var producedMessage in producedMessages)
        {
            var messageCopies = consumedMessages.Snapshot().Count(x => x.Counter == producedMessage.Counter && x.Value == producedMessage.Value);
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
            mbb.Produce<EchoRequest>(x => x.DefaultTopic(topic));
            mbb.Handle<EchoRequest, EchoResponse>(x => x.Topic(topic).Instances(2));
        });

        await BasicReqResp().ConfigureAwait(false);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BasicReqRespWithoutRespOnTopic(bool enableSerialization)
    {
        _settings.EnableMessageSerialization = enableSerialization;

        var topic = "test-echo";

        AddBusConfiguration(mbb =>
        {
            mbb.Produce<SomeRequestWithoutResponse>(x => x.DefaultTopic(topic));
            mbb.Handle<SomeRequestWithoutResponse>(x => x.Topic(topic).Instances(2));
        });

        await BasicReqRespWithoutResp().ConfigureAwait(false);
    }

    private async Task BasicReqResp()
    {
        // arrange
        var messageBus = MessageBus;

        // act

        // publish
        var stopwatch = Stopwatch.StartNew();

        var requests = Enumerable
            .Range(0, NumberOfMessages)
            .Select(i => new EchoRequest { Index = i, Message = $"Echo {i}" })
            .ToList();

        var responses = new ConcurrentBag<Tuple<EchoRequest, EchoResponse>>();
        var responseTasks = requests.Select(async req =>
        {
            var resp = await messageBus.Send(req).ConfigureAwait(false);
            responses.Add(Tuple.Create(req, resp));
        });
        await Task.WhenAll(responseTasks).ConfigureAwait(false);

        stopwatch.Stop();
        Logger.LogInformation("Published and received {0} messages in {1}", responses.Count, stopwatch.Elapsed);

        // assert

        // all messages got back
        responses.Count.Should().Be(NumberOfMessages);
        responses.All(x => x.Item1.Message == x.Item2.Message).Should().BeTrue();
    }

    private async Task BasicReqRespWithoutResp()
    {
        // arrange
        var messageBus = MessageBus;

        // act

        // publish
        var stopwatch = Stopwatch.StartNew();

        var requests = Enumerable
            .Range(0, NumberOfMessages)
            .Select(i => new SomeRequestWithoutResponse(Guid.NewGuid()))
            .ToList();

        var responses = new ConcurrentBag<SomeRequestWithoutResponse>();
        var responseTasks = requests.Select(async req =>
        {
            await messageBus.Send(req).ConfigureAwait(false);
            responses.Add(req);
        });
        await Task.WhenAll(responseTasks).ConfigureAwait(false);

        stopwatch.Stop();
        Logger.LogInformation("Published and received {0} messages in {1}", responses.Count, stopwatch.Elapsed);

        // assert

        // all messages got back
        responses.Count.Should().Be(NumberOfMessages);
    }

    internal record PingMessage
    {
        public int Counter { get; init; }
        public Guid Value { get; init; }
    }

    internal record PingConsumer(TestEventCollector<PingMessage> Messages) : IConsumer<PingMessage>
    {
        public Task OnHandle(PingMessage message)
        {
            Messages.Add(message);

            Console.WriteLine("Got message {0}.", message.Counter);
            return Task.CompletedTask;
        }
    }

    internal record EchoRequest : IRequest<EchoResponse>
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
        public Task<EchoResponse> OnHandle(EchoRequest request) =>
            Task.FromResult(new EchoResponse { Message = request.Message });
    }
}
