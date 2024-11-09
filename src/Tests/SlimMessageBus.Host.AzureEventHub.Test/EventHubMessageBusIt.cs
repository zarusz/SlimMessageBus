namespace SlimMessageBus.Host.AzureEventHub.Test;

using System.Collections.Concurrent;
using System.Diagnostics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using SlimMessageBus.Host;
using SlimMessageBus.Host.Serialization.Json;
using SlimMessageBus.Host.Test.Common.IntegrationTest;

/// <summary>
/// Runs the integration tests for the <see cref="EventHubMessageBus"/>.
/// Notice that this test needs to run against a real Azure Event Hub infrastructure.
/// Inside the GitHub Actions pipeline, the Azure Event Hub infrastructure is shared, and so if tests are run in parallel they might affect each other (flaky tests).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Transport", "AzureEventHub")]
public class EventHubMessageBusIt(ITestOutputHelper output) : BaseIntegrationTest<EventHubMessageBusIt>(output)
{
    private const int NumberOfMessages = 100;

    protected override void SetupServices(ServiceCollection services, IConfigurationRoot configuration)
    {
        services.AddSlimMessageBus(mbb =>
        {
            mbb.WithProviderEventHub(cfg =>
            {
                cfg.ConnectionString = Secrets.Service.PopulateSecrets(configuration["Azure:EventHub"]);
                cfg.StorageConnectionString = Secrets.Service.PopulateSecrets(configuration["Azure:Storage"]);
                cfg.StorageBlobContainerName = configuration["Azure:ContainerName"];
                cfg.EventHubProducerClientOptionsFactory = (path) => new Azure.Messaging.EventHubs.Producer.EventHubProducerClientOptions
                {
                    Identifier = $"MyService_{Guid.NewGuid()}"
                };
                cfg.EventHubProcessorClientOptionsFactory = (consumerParams) => new Azure.Messaging.EventHubs.EventProcessorClientOptions
                {
                    // Allow the test to be repeatable - force partition lease rebalancing to happen faster
                    LoadBalancingUpdateInterval = TimeSpan.FromSeconds(2),
                    PartitionOwnershipExpirationInterval = TimeSpan.FromSeconds(5),
                };
            });
            mbb.AddServicesFromAssemblyContaining<PingConsumer>();
            mbb.AddJsonSerializer();

            ApplyBusConfiguration(mbb);
        });

        services.AddSingleton<ConcurrentBag<PingMessage>>();
    }

    public IMessageBus MessageBus => ServiceProvider.GetRequiredService<IMessageBus>();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BasicPubSub(bool bulkProduce)
    {
        // arrange
        var hubName = "test-ping";
        AddBusConfiguration(mbb =>
        {
            mbb
            .Produce<PingMessage>(x => x.DefaultPath(hubName).KeyProvider(m => (m.Counter % 2) == 0 ? "even" : "odd"))
            .Consume<PingMessage>(x => x.Path(hubName)
                                        .Group("subscriber") // ensure consumer group exists on the event hub
                                        .WithConsumerOfContext<PingConsumer>()
                                        .CheckpointAfter(TimeSpan.FromSeconds(10))
                                        .CheckpointEvery(50)
                                        .Instances(2));
        });

        var consumedMessages = ServiceProvider.GetRequiredService<ConcurrentBag<PingMessage>>();
        var messageBus = MessageBus;

        // act

        // consume all messages that might be on the queue/subscription
        await consumedMessages.WaitUntilArriving(newMessagesTimeout: 5);
        consumedMessages.Clear();

        // publish
        var stopwatch = Stopwatch.StartNew();

        var messages = Enumerable
            .Range(0, NumberOfMessages)
            .Select(i => new PingMessage { Counter = i, Timestamp = DateTime.UtcNow })
            .ToList();

        if (bulkProduce)
        {
            await messageBus.Publish(messages);
        }
        else
        {
            foreach (var m in messages)
            {
                await messageBus.Publish(m);
            }
        }

        stopwatch.Stop();
        Logger.LogInformation("Published {PublishedMessageCount} messages in {Duration}", messages.Count, stopwatch.Elapsed);

        // consume
        stopwatch.Restart();
        await consumedMessages.WaitUntilArriving(newMessagesTimeout: 5);
        stopwatch.Stop();
        Logger.LogInformation("Consumed {ConsumedMessageCount} messages in {Duration}", consumedMessages.Count, stopwatch.Elapsed);

        // assert

        // all messages got back
        consumedMessages.Count.Should().Be(messages.Count);
    }

    [Fact]
    public async Task BasicReqResp()
    {
        // arrange

        // ensure the topic has 2 partitions
        var topic = "test-echo";
        var echoRequestHandler = new EchoRequestHandler();

        AddBusConfiguration(mbb =>
        {
            mbb
                .Produce<EchoRequest>(x =>
                {
                    x.DefaultTopic(topic);
                })
                .Handle<EchoRequest, EchoResponse>(x => x.Path(topic)
                                                         .Group("handler") // ensure consumer group exists on the event hub
                                                         .WithHandler<EchoRequestHandler>()
                                                         .Instances(2))
                .ExpectRequestResponses(x =>
                {
                    x.ReplyToTopic("test-echo-resp");
                    x.Group("response-reader"); // ensure consumer group exists on the event hub
                    x.DefaultTimeout(TimeSpan.FromSeconds(30));
                });
        });

        var messageBus = MessageBus;

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
}

public class PingMessage
{
    public DateTime Timestamp { get; set; }
    public int Counter { get; set; }

    #region Overrides of Object

    public override string ToString() => $"PingMessage(Counter={Counter}, Timestamp={Timestamp})";

    #endregion
}

public class PingConsumer(ILogger<PingConsumer> logger, ConcurrentBag<PingMessage> messages) : IConsumer<IConsumerContext<PingMessage>>
{
    private readonly ILogger _logger = logger;
    private readonly ConcurrentBag<PingMessage> _messages = messages;

    public Task OnHandle(IConsumerContext<PingMessage> context, CancellationToken cancellationToken)
    {
        _messages.Add(context.Message);

        var msg = context.GetTransportMessage();

        _logger.LogInformation("Got message {Message:000} on topic {Path} offset {Offset} partition key {PartitionKey}.", context.Message.Counter, context.Path, msg.Offset, msg.PartitionKey);
        return Task.CompletedTask;
    }
}

public class EchoRequest : IRequest<EchoResponse>
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
    public Task<EchoResponse> OnHandle(EchoRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new EchoResponse { Message = request.Message });
    }
}