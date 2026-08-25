namespace SlimMessageBus.Host.GooglePubSub.Test;

[Trait("Category", "Integration")]
[Trait("Transport", "GooglePubSub")]
[Collection(nameof(GooglePubSubCollection))]
public class GooglePubSubMessageBusIt(ITestOutputHelper output, GooglePubSubFixture fixture)
    : BaseIntegrationTest<GooglePubSubMessageBusIt>(output)
{
    private readonly string _topic = $"orders-{Guid.NewGuid():N}";
    private readonly string _subscription = $"orders-worker-{Guid.NewGuid():N}";

    protected override void SetupServices(ServiceCollection services, IConfigurationRoot configuration)
    {
        services.AddSlimMessageBus(mbb =>
        {
            mbb.WithProviderGooglePubSub(settings =>
            {
                settings.ProjectId = "slim-message-bus-tests";
                ConfigureEmulatorClients(settings, fixture.EmulatorEndpoint);
            });
            mbb.Produce<OrderSubmitted>(x => x.DefaultTopic(_topic));
            mbb.Consume<OrderSubmitted>(x => x.Topic(_topic).SubscriptionName(_subscription));
            mbb.AddServicesFromAssemblyContaining<OrderSubmittedConsumer>();
            mbb.AddJsonSerializer();
        });

        services.AddSingleton<TestEventCollector<OrderSubmitted>>();
    }

    [Fact]
    public async Task ProvisionsTopologyAndDeliversPublishedMessage()
    {
        var messageBus = ServiceProvider.GetRequiredService<IMessageBus>();
        var consumedMessages = ServiceProvider.GetRequiredService<TestEventCollector<OrderSubmitted>>();
        var message = new OrderSubmitted(Guid.NewGuid(), 42);

        await EnsureConsumersStarted();
        await messageBus.Publish(message, headers: new Dictionary<string, object> { ["source"] = "integration-test" });
        await consumedMessages.WaitUntilArriving(newMessagesTimeout: 30, expectedCount: 1);

        consumedMessages.Snapshot().Should().ContainSingle().Which.Should().Be(message);
    }

    private static void ConfigureEmulatorClients(GooglePubSubMessageBusSettings settings, string endpoint)
    {
        settings.PublisherClientFactory = (_, topic, cancellationToken) => new PublisherClientBuilder
        {
            TopicName = topic,
            Endpoint = endpoint,
            ChannelCredentials = ChannelCredentials.Insecure
        }.BuildAsync(cancellationToken);
        settings.SubscriberClientFactory = (_, subscription, cancellationToken) => new SubscriberClientBuilder
        {
            SubscriptionName = subscription,
            Endpoint = endpoint,
            ChannelCredentials = ChannelCredentials.Insecure
        }.BuildAsync(cancellationToken);
        settings.PublisherServiceApiClientFactory = (_, cancellationToken) => new PublisherServiceApiClientBuilder
        {
            Endpoint = endpoint,
            ChannelCredentials = ChannelCredentials.Insecure
        }.BuildAsync(cancellationToken);
        settings.SubscriberServiceApiClientFactory = (_, cancellationToken) => new SubscriberServiceApiClientBuilder
        {
            Endpoint = endpoint,
            ChannelCredentials = ChannelCredentials.Insecure
        }.BuildAsync(cancellationToken);
    }

    public record OrderSubmitted(Guid OrderId, int Quantity);

    public class OrderSubmittedConsumer(TestEventCollector<OrderSubmitted> messages) : IConsumer<OrderSubmitted>
    {
        public Task OnHandle(OrderSubmitted message, CancellationToken cancellationToken)
        {
            messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
