namespace SlimMessageBus.Host.Memory.Test;

using System.Collections.Concurrent;
using System.Diagnostics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using SlimMessageBus.Host;
using SlimMessageBus.Host.Serialization.Json;
using SlimMessageBus.Host.Test.Common.IntegrationTest;

[Trait("Category", "Integration")]
[Trait("Transport", "Memory")]
public class MemoryMessageBusIt(ITestOutputHelper output) : BaseIntegrationTest<MemoryMessageBusIt>(output)
{
    private const int NumberOfMessages = 1023;

    private bool _enableSerialization = false;
    private bool _enableBlockingPublish = true;

    protected override void SetupServices(ServiceCollection services, IConfigurationRoot configuration)
    {
        // doc:fragment:ExampleSetup
        services.AddSlimMessageBus(mbb =>
        {
            mbb
                .WithProviderMemory(cfg =>
                {
                    cfg.EnableMessageSerialization = _enableSerialization;
                    cfg.EnableBlockingPublish = _enableBlockingPublish;
                })
                .AddServicesFromAssemblyContaining<PingConsumer>()
                .AddJsonSerializer();
        });
        // doc:fragment:ExampleSetup

        services.AddSlimMessageBus(ApplyBusConfiguration);

        services.AddSingleton<TestEventCollector<PingMessage>>();
        services.AddScoped<SafeCounter>();
    }

    public IMessageBus MessageBus => ServiceProvider.GetRequiredService<IMessageBus>();

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task BasicPubSubOnTopic(bool enableSerialization, bool enableBlockingPublish)
    {
        _enableSerialization = enableSerialization;
        _enableBlockingPublish = enableBlockingPublish;

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

        await BasicPubSub(subscribers);
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
        await Task.WhenAll(messageTasks);

        stopwatch.Stop();
        Logger.LogInformation("Published {MessageCount} messages in {ElapsedTime}", producedMessages.Count, stopwatch.Elapsed);

        // consume
        stopwatch.Restart();
        await consumedMessages.WaitUntilArriving(expectedCount: subscribers * producedMessages.Count);
        stopwatch.Stop();

        Logger.LogInformation("Consumed {MessageCount} messages in {ElapsedTime}", consumedMessages.Count, stopwatch.Elapsed);

        // assert

        // ensure all messages arrived 
        // ... the count should match

        var consumedMessagesSnapshot = consumedMessages.Snapshot();

        consumedMessagesSnapshot
            .Should().HaveCount(subscribers * producedMessages.Count);

        if (_enableBlockingPublish)
        {
            // When blocking publish is enabled, the message will be process in the same DI scope as the publish, hence the counter will run be only created once
            // This checks that the per message scope is not created when publish is blocking
            consumedMessagesSnapshot
                .OrderBy(x => x.ConsumerCounter).Skip(1) // the first element will be 1
                .Should().AllSatisfy(x => x.ConsumerCounter.Should().BeGreaterThan(1));
        }
        else
        {
            // When blocking publish is disabled, each message will be executed in its own DI scope hence the counter instance will start from 0
            // This checks that the per message scope is created when publish is not blocking
            consumedMessagesSnapshot
                .Should().AllSatisfy(x => x.ConsumerCounter.Should().Be(1));
        }

        // ... the content should match
        foreach (var producedMessage in producedMessages)
        {
            consumedMessagesSnapshot
                .Count(x => x.Counter == producedMessage.Counter && x.Value == producedMessage.Value)
                .Should().Be(subscribers);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BasicReqRespOnTopic(bool enableSerialization)
    {
        _enableSerialization = enableSerialization;

        var topic = "test-echo";

        AddBusConfiguration(mbb =>
        {
            mbb.Produce<EchoRequest>(x => x.DefaultTopic(topic));
            mbb.Handle<EchoRequest, EchoResponse>(x => x.Topic(topic).Instances(2));
        });

        await BasicReqResp();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BasicReqRespWithoutRespOnTopic(bool enableSerialization)
    {
        _enableSerialization = enableSerialization;

        var topic = "test-echo";

        AddBusConfiguration(mbb =>
        {
            mbb.Produce<SomeRequestWithoutResponse>(x => x.DefaultTopic(topic));
            mbb.Handle<SomeRequestWithoutResponse>(x => x.Topic(topic).Instances(2));
        });

        await BasicReqRespWithoutResp();
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
        Logger.LogInformation("Published and received {MessageCount} messages in {ElapsedTime}", responses.Count, stopwatch.Elapsed);

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
        Logger.LogInformation("Published and received {MessageCount} messages in {ElapsedTime}", responses.Count, stopwatch.Elapsed);

        // assert

        // all messages got back
        responses.Count.Should().Be(NumberOfMessages);
    }

    internal record PingMessage
    {
        public int Counter { get; init; }
        public Guid Value { get; init; }
        public int ConsumerCounter { get; set; }
    }

    internal record PingConsumer(TestEventCollector<PingMessage> Messages, SafeCounter SafeCounter) : IConsumer<PingMessage>
    {
        public Task OnHandle(PingMessage message, CancellationToken cancellationToken)
        {
            message.ConsumerCounter = SafeCounter.NextValue();
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
        public Task<EchoResponse> OnHandle(EchoRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new EchoResponse { Message = request.Message });
    }
}

public class SafeCounter
{
    private int _value;

    public int NextValue()
        => Interlocked.Increment(ref _value);
}