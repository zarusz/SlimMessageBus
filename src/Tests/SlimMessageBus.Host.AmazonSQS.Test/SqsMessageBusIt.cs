namespace SlimMessageBus.Host.AmazonSQS.Test;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using Amazon.SQS.Model;

using Microsoft.Extensions.Logging;

using SlimMessageBus.Host.Serialization.SystemTextJson;

/// <summary>
/// Runs the integration tests for the <see cref="SqsMessageBus"/>.
/// Notice that this test needs to run against a real Amazon SQS infrastructure.
/// Inside the GitHub Actions pipeline, the Amazon SQS infrastructure is shared, and this tests attempts to isolate itself by using unique queue names.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Transport", "AmazonSQS")]
public class SqsMessageBusIt(ITestOutputHelper output) : BaseIntegrationTest<SqsMessageBusIt>(output)
{
    private const int NumberOfMessages = 100;
    private const string QueueNamePrefix = "SMB";
    private const string CreatedDateTag = "CreatedDate";

    protected override void SetupServices(ServiceCollection services, IConfigurationRoot configuration)
    {
        var today = DateTime.UtcNow.Date.ToString("o");

        services.AddSingleton<TestEventCollector<TestEvent>>();

        services.AddSlimMessageBus((mbb) =>
        {
            mbb.AddServicesFromAssemblyContaining<PingConsumer>();
            mbb.AddJsonSerializer();
            ApplyBusConfiguration(mbb);
        });

        void AdditionalSqsSetup(SqsMessageBusSettings cfg)
        {
            cfg.TopologyProvisioning.CreateQueueOptions = opts =>
            {
                // Tag the queue with the creation date
                opts.Tags.Add(CreatedDateTag, today);
            };
            cfg.TopologyProvisioning.OnProvisionTopology = async (client, provision) =>
            {
                // Remove all older test queues (SQS does not support queue auto deletion)
                var r = await client.ListQueuesAsync(QueueNamePrefix);
                foreach (var queueUrl in r.QueueUrls)
                {
                    try
                    {
                        var tagsResponse = await client.ListQueueTagsAsync(new ListQueueTagsRequest { QueueUrl = queueUrl });
                        if (!tagsResponse.Tags.TryGetValue(CreatedDateTag, out var createdDateTag) || createdDateTag != today)
                        {
                            await client.DeleteQueueAsync(queueUrl);
                        }
                    }
                    catch (QueueDoesNotExistException)
                    {
                        // ignore, other tests might already have deleted the queue
                    }
                }
                await provision();
            };
        }

        var accessKey = Secrets.Service.PopulateSecrets(configuration["Amazon:AccessKey"]);
        var secretAccessKey = Secrets.Service.PopulateSecrets(configuration["Amazon:SecretAccessKey"]);

        var roleArn = Secrets.Service.PopulateSecrets(configuration["Amazon:RoleArn"]);
        var roleSessionName = Secrets.Service.PopulateSecrets(configuration["Amazon:RoleSessionName"]);

        // doc:fragment:ExampleSetup
        services.AddSlimMessageBus((mbb) =>
        {
            mbb.WithProviderAmazonSQS(cfg =>
            {
                cfg.UseRegion(Amazon.RegionEndpoint.EUCentral1);

                // Use static credentials: https://docs.aws.amazon.com/sdkref/latest/guide/access-iam-users.html
                cfg.UseCredentials(accessKey, secretAccessKey);

                // Use temporary credentials: https://docs.aws.amazon.com/IAM/latest/UserGuide/id_credentials_temp_use-resources.html#RequestWithSTS
                //cfg.UseTemporaryCredentials(roleArn, roleSessionName);

                AdditionalSqsSetup(cfg);
            });
        });
        // doc:fragment:ExampleSetup
    }

    public IMessageBus MessageBus => ServiceProvider.GetRequiredService<IMessageBus>();

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task BasicQueue(bool fifo, bool bulkProduce)
    {
        var queue = string.Concat(QueueName(), fifo ? ".fifo" : string.Empty);
        AddBusConfiguration(mbb =>
        {
            mbb
            .Produce<PingMessage>(x =>
            {
                x.DefaultQueue(queue);
                if (fifo)
                {
                    x.EnableFifo(f => f
                        .DeduplicationId((m, h) => (m.Counter + 1000).ToString())
                        .GroupId((m, h) => m.Counter % 2 == 0 ? "even" : "odd")
                    );
                }
            })
            .Consume<PingMessage>(x => x
                    .Queue(queue)
                    .WithConsumer<PingConsumer>()
                    .WithConsumer<PingDerivedConsumer, PingDerivedMessage>()
                    .Instances(20));
        });

        await BasicProducerConsumer(1, bulkProduce: bulkProduce);
    }

    public class TestData
    {
        public List<PingMessage> ProducedMessages { get; set; }
        public IReadOnlyCollection<TestEvent> ConsumedMessages { get; set; }
    }

    private async Task BasicProducerConsumer(int expectedMessageCopies, Action<TestData> additionalAssertion = null, bool bulkProduce = false)
    {
        // arrange
        var testMetric = ServiceProvider.GetRequiredService<TestMetric>();
        var consumedMessages = ServiceProvider.GetRequiredService<TestEventCollector<TestEvent>>();

        var messageBus = MessageBus;

        // act

        // publish
        var stopwatch = Stopwatch.StartNew();

        var producedMessages = Enumerable
            .Range(0, NumberOfMessages)
            .Select(i => i % 2 == 0 ? new PingMessage(i) : new PingDerivedMessage(i))
            .ToList();

        if (bulkProduce)
        {
            await messageBus.Publish(producedMessages);
        }
        else
        {
            foreach (var producedMessage in producedMessages)
            {
                // Send them in order
                await messageBus.Publish(producedMessage);
            }
        }

        stopwatch.Stop();
        Logger.LogInformation("Published {Count} messages in {Elapsed}", producedMessages.Count, stopwatch.Elapsed);

        // consume
        stopwatch.Restart();

        await consumedMessages.WaitUntilArriving(newMessagesTimeout: 5);

        stopwatch.Stop();

        // assert

        // ensure number of instances of consumers created matches
        var expectedConsumedCount = producedMessages.Count + producedMessages.OfType<PingDerivedMessage>().Count();
        testMetric.CreatedConsumerCount.Should().Be(expectedConsumedCount * expectedMessageCopies);
        consumedMessages.Count.Should().Be(expectedConsumedCount * expectedMessageCopies);

        // ... the content should match
        foreach (var producedMessage in producedMessages)
        {
            var messageCopies = consumedMessages.Snapshot()
                .Count(x =>
                    x.Message.Counter == producedMessage.Counter
                    && x.Message.Value == producedMessage.Value
                    /*&& x.MessageId == GetMessageId(x.Message)*/);
            messageCopies.Should().Be((producedMessage is PingDerivedMessage ? 2 : 1) * expectedMessageCopies);
        }

        additionalAssertion?.Invoke(new TestData { ProducedMessages = producedMessages, ConsumedMessages = consumedMessages.Snapshot() });
    }

    [Fact]
    public async Task BasicReqRespOnQueue()
    {
        var queue = QueueName();
        var responseQueue = $"{queue}-resp";

        AddBusConfiguration(mbb =>
        {
            mbb.Produce<EchoRequest>(x =>
            {
                x.DefaultQueue(queue);
            })
            .Handle<EchoRequest, EchoResponse>(x => x.Queue(queue)
                .WithHandler<EchoRequestHandler>()
                .Instances(20))
            .ExpectRequestResponses(x =>
            {
                x.ReplyToQueue(responseQueue);
                x.DefaultTimeout(TimeSpan.FromSeconds(60));
            });
        });
        await BasicReqResp();
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
        Logger.LogInformation("Published and received {Count} messages in {Elapsed}", responses.Count, stopwatch.Elapsed);

        // assert

        // all messages got back
        responses.Count.Should().Be(NumberOfMessages);
        responses.All(x => x.Item1.Message == x.Item2.Message).Should().BeTrue();
    }

    private static string QueueName([CallerMemberName] string testName = null)
        => $"{QueueNamePrefix}_{DateTimeOffset.UtcNow.Ticks}_{testName}";
}

public record TestEvent(PingMessage Message);

public record PingMessage(int Counter)
{
    public Guid Value { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public record PingDerivedMessage(int Counter) : PingMessage(Counter);

public class PingConsumer : IConsumer<PingMessage>, IConsumerWithContext
{
    private readonly ILogger _logger;
    private readonly TestEventCollector<TestEvent> _messages;

    public PingConsumer(ILogger<PingConsumer> logger, TestEventCollector<TestEvent> messages, TestMetric testMetric)
    {
        _logger = logger;
        _messages = messages;
        testMetric.OnCreatedConsumer();
    }

    public IConsumerContext Context { get; set; }

    public Task OnHandle(PingMessage message, CancellationToken cancellationToken)
    {
        var transportMessage = Context.GetTransportMessage();

        _messages.Add(new(message));

        _logger.LogInformation("Got message {Counter:000} on path {Path} message id {MessageId}.", message.Counter, Context.Path, transportMessage.MessageId);
        return Task.CompletedTask;
    }
}

public class PingDerivedConsumer : IConsumer<PingDerivedMessage>, IConsumerWithContext
{
    private readonly ILogger _logger;
    private readonly TestEventCollector<TestEvent> _messages;

    public PingDerivedConsumer(ILogger<PingDerivedConsumer> logger, TestEventCollector<TestEvent> messages, TestMetric testMetric)
    {
        _logger = logger;
        _messages = messages;
        testMetric.OnCreatedConsumer();
    }

    public IConsumerContext Context { get; set; }

    public Task OnHandle(PingDerivedMessage message, CancellationToken cancellationToken)
    {
        var transportMessage = Context.GetTransportMessage();

        _messages.Add(new(message));

        _logger.LogInformation("Got message {Counter:000} on path {Path} message id {MessageId}.", message.Counter, Context.Path, transportMessage.MessageId);
        return Task.CompletedTask;
    }
}

public record EchoRequest : IRequest<EchoResponse>
{
    public int Index { get; set; }
    public string Message { get; set; }
}

public record EchoResponse(string Message);

public class EchoRequestHandler : IRequestHandler<EchoRequest, EchoResponse>
{
    public EchoRequestHandler(TestMetric testMetric)
    {
        testMetric.OnCreatedConsumer();
    }

    public Task<EchoResponse> OnHandle(EchoRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new EchoResponse(request.Message));
}