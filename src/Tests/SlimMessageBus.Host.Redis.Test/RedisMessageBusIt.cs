namespace SlimMessageBus.Host.Redis.Test;

using System.Collections.Concurrent;
using System.Diagnostics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using SlimMessageBus.Host;
using SlimMessageBus.Host.Serialization.Json;
using SlimMessageBus.Host.Test.Common.IntegrationTest;

[Trait("Category", "Integration")]
[Trait("Transport", "Redis")]
public class RedisMessageBusIt(ITestOutputHelper output) : BaseIntegrationTest<RedisMessageBusIt>(output)
{
    private const int NumberOfMessages = 77;

    protected override void SetupServices(ServiceCollection services, IConfigurationRoot configuration)
    {
        services.AddSlimMessageBus(mbb =>
        {
            var connectionString = Secrets.Service.PopulateSecrets(configuration["Redis:ConnectionString"]);

            mbb.WithProviderRedis(cfg =>
            {
                cfg.ConnectionString = connectionString;
                cfg.OnDatabaseConnected = (database) =>
                {
                    // Upon connect clear the redis list with the specified keys
                    database.KeyDelete("test-echo-queue");
                    database.KeyDelete("test-echo-queue-resp");
                };
            });

            mbb.AddServicesFromAssemblyContaining<PingConsumer>();
            mbb.AddJsonSerializer();

            ApplyBusConfiguration(mbb);
        });

        services.AddSingleton<TestEventCollector<PingMessage>>();
    }

    public IMessageBus MessageBus => ServiceProvider.GetRequiredService<IMessageBus>();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BasicPubSubOnTopic(bool bulkProduce)
    {
        var concurrency = 2;
        var consumers = 2;
        var topic = "test-ping";

        AddBusConfiguration(mbb =>
        {
            mbb
                .Produce<PingMessage>(x =>
                {
                    x.DefaultTopic(topic);
                })
                .Do(builder => Enumerable.Range(0, consumers).ToList().ForEach(i =>
                {
                    builder.Consume<PingMessage>(x => x
                        .Topic(topic)
                        .WithConsumer<PingConsumer>()
                        .Instances(concurrency));
                }));
        });

        await BasicPubSub(consumers, bulkProduce);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BasicPubSubOnQueue(bool bulkProduce)
    {
        var concurrency = 2;
        var consumers = 2;
        var queue = "test-ping-queue";

        AddBusConfiguration(mbb =>
        {
            mbb
                .Produce<PingMessage>(x =>
                {
                    x.DefaultQueue(queue);
                })
                .Do(builder => Enumerable.Range(0, consumers).ToList().ForEach(i =>
                {
                    builder.Consume<PingMessage>(x => x
                        .Queue(queue)
                        .WithConsumer<PingConsumer>()
                        .Instances(concurrency));
                }));
        });

        await BasicPubSub(consumers, bulkProduce);
    }

    private async Task BasicPubSub(int expectedMessageCopies, bool bulkProduce)
    {
        // arrange
        var messageBus = MessageBus;
        var consumedMessages = ServiceProvider.GetRequiredService<TestEventCollector<PingMessage>>();

        // ensure the consumers are warm
        //while (!messageBus.IsStarted) await Task.Delay(200);

        // act

        // consume all messages that might be on the queue/subscription
        await consumedMessages.WaitUntilArriving(newMessagesTimeout: 4);
        consumedMessages.Clear();

        // publish
        var stopwatch = Stopwatch.StartNew();

        var producedMessages = Enumerable
            .Range(0, NumberOfMessages)
            .Select(i => new PingMessage(i, Guid.NewGuid()))
            .ToList();


        if (bulkProduce)
        {
            await messageBus.Publish(producedMessages);
        }
        else
        {
            var messageTasks = producedMessages.Select(m => messageBus.Publish(m));
            // wait until all messages are sent
            await Task.WhenAll(messageTasks).ConfigureAwait(false);
        }

        stopwatch.Stop();
        Logger.LogInformation("Published {MessageCount} messages in {Duration}", producedMessages.Count, stopwatch.Elapsed);

        // consume
        stopwatch.Restart();
        await consumedMessages.WaitUntilArriving(expectedCount: expectedMessageCopies * producedMessages.Count);
        stopwatch.Stop();

        Logger.LogInformation("Consumed {MessageCount} messages in {Duration}", consumedMessages, stopwatch.Elapsed);

        // assert

        // ensure all messages arrived 
        // ... the count should match
        consumedMessages.Count.Should().Be(producedMessages.Count * expectedMessageCopies);
        // ... the content should match
        foreach (var producedMessage in producedMessages)
        {
            var messageCopies = consumedMessages.Snapshot().Count(x => x.Counter == producedMessage.Counter && x.Value == producedMessage.Value);
            messageCopies.Should().Be(expectedMessageCopies);
        }
    }

    [Fact]
    public async Task BasicReqRespOnTopic()
    {
        var topic = "test-echo";

        AddBusConfiguration(mbb =>
        {
            mbb
                .Produce<EchoRequest>(x =>
                {
                    x.DefaultTopic(topic);
                    x.DefaultTimeout(TimeSpan.FromSeconds(30));
                })
                .Handle<EchoRequest, EchoResponse>(x => x.Topic(topic)
                    .WithHandler<EchoRequestHandler>()
                    .Instances(2))
                .ExpectRequestResponses(x =>
                {
                    x.ReplyToTopic("test-echo-resp");
                    x.DefaultTimeout(TimeSpan.FromSeconds(30));
                });
        });

        await BasicReqResp();
    }

    [Fact]
    public async Task BasicReqRespOnQueue()
    {
        var queue = "test-echo-queue";

        AddBusConfiguration(mbb =>
        {
            mbb
                .Produce<EchoRequest>(x =>
                {
                    x.DefaultQueue(queue);
                    x.DefaultTimeout(TimeSpan.FromSeconds(30));
                })
                .Handle<EchoRequest, EchoResponse>(x => x.Queue(queue)
                    .WithHandler<EchoRequestHandler>()
                    .Instances(2))
                .ExpectRequestResponses(x =>
                {
                    x.ReplyToQueue("test-echo-queue-resp");
                    x.DefaultTimeout(TimeSpan.FromSeconds(30));
                });
        });

        await BasicReqResp();
    }

    private async Task BasicReqResp()
    {
        // arrange
        var consumer = new EchoRequestHandler();

        var messageBus = MessageBus;

        await EnsureConsumersStarted();

        // act

        // publish
        var stopwatch = Stopwatch.StartNew();

        var requests = Enumerable
            .Range(0, NumberOfMessages)
            .Select(i => new EchoRequest(i, $"Echo {i}"))
            .ToList();

        var responses = new ConcurrentBag<(EchoRequest Request, EchoResponse Response)>();
        var responseTasks = requests.Select(async req =>
        {
            var resp = await messageBus.Send<EchoResponse, EchoRequest>(req).ConfigureAwait(false);
            Logger.LogDebug("Received response for index {ResponseIndex:000}", req.Index);
            responses.Add((req, resp));
        });
        await Task.WhenAll(responseTasks).ConfigureAwait(false);

        stopwatch.Stop();
        Logger.LogInformation("Published and received {MessageCount} messages in {Duration}", responses.Count, stopwatch.Elapsed);

        // assert

        // all messages got back
        responses.Count.Should().Be(NumberOfMessages);
        responses.All(x => x.Request.Message == x.Response.Message).Should().BeTrue();
    }

    private record PingMessage(int Counter, Guid Value);

    private class PingConsumer(ILogger<PingConsumer> logger, TestEventCollector<PingMessage> messages)
        : IConsumer<PingMessage>, IConsumerWithContext
    {
        public IConsumerContext Context { get; set; }

        #region Implementation of IConsumer<in PingMessage>

        public Task OnHandle(PingMessage message, CancellationToken cancellationToken)
        {
            messages.Add(message);
            logger.LogInformation("Got message {MessageCounter} on topic {Topic}.", message.Counter, Context.Path);
            return Task.CompletedTask;
        }

        #endregion
    }

    private record EchoRequest(int Index, string Message);

    private class EchoResponse
    {
        public string Message { get; set; }

        public override string ToString() => $"EchoResponse(Message={Message})";
    }

    private class EchoRequestHandler : IRequestHandler<EchoRequest, EchoResponse>
    {
        public Task<EchoResponse> OnHandle(EchoRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new EchoResponse { Message = request.Message });
    }
}