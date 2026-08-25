namespace SlimMessageBus.Host.GooglePubSub.Test;

public class GooglePubSubMessageBusSettingsValidationServiceTests
{
    private record TestMessage;

    private readonly MessageBusSettings _settings = new()
    {
        Name = "TestBus",
        ServiceProvider = Mock.Of<IServiceProvider>()
    };

    [Fact]
    public void ProjectId_IsRequired()
    {
        var validator = new GooglePubSubMessageBusSettingsValidationService(
            _settings,
            new GooglePubSubMessageBusSettings { ProjectId = null });

        Action act = validator.AssertSettings;
        act.Should().Throw<ConfigurationMessageBusException>()
            .WithMessage("*ProjectId*");
    }

    [Theory]
    [InlineData("publisher")]
    [InlineData("subscriber")]
    [InlineData("headers")]
    public void RequiredProviderServices_AreValidated(string missingService)
    {
        var providerSettings = new GooglePubSubMessageBusSettings { ProjectId = "project" };
        switch (missingService)
        {
            case "publisher":
                providerSettings.PublisherClientFactory = null;
                break;
            case "subscriber":
                providerSettings.SubscriberClientFactory = null;
                break;
            case "headers":
                providerSettings.HeaderSerializer = null;
                break;
        }
        var validator = new GooglePubSubMessageBusSettingsValidationService(_settings, providerSettings);

        Action act = validator.AssertSettings;

        act.Should().Throw<ConfigurationMessageBusException>();
    }

    [Fact]
    public void TopicConsumer_RequiresSubscriptionName()
    {
        _ = new ConsumerBuilder<TestMessage>(_settings)
            .Topic("orders")
            .WithConsumer<IConsumer<TestMessage>>();
        var validator = new GooglePubSubMessageBusSettingsValidationService(
            _settings,
            new GooglePubSubMessageBusSettings { ProjectId = "project" });

        Action act = validator.AssertSettings;
        act.Should().Throw<ConfigurationMessageBusException>()
            .WithMessage("*SubscriptionName*");
    }

    [Fact]
    public void Producer_MustTargetTopic()
    {
        _settings.Producers.Add(new ProducerSettings
        {
            MessageType = typeof(TestMessage),
            DefaultPath = "orders",
            PathKind = PathKind.Queue
        });
        var validator = new GooglePubSubMessageBusSettingsValidationService(
            _settings,
            new GooglePubSubMessageBusSettings { ProjectId = "project" });

        Action act = validator.AssertSettings;

        act.Should().Throw<ConfigurationMessageBusException>()
            .WithMessage("*Topic*");
    }

    [Fact]
    public void Consumer_MustTargetTopic()
    {
        var consumerBuilder = new ConsumerBuilder<TestMessage>(_settings);
        consumerBuilder.ConsumerSettings.Path = "orders";
        consumerBuilder.ConsumerSettings.PathKind = PathKind.Queue;
        _ = consumerBuilder
            .SubscriptionName("orders-worker")
            .WithConsumer<IConsumer<TestMessage>>();
        var validator = new GooglePubSubMessageBusSettingsValidationService(
            _settings,
            new GooglePubSubMessageBusSettings { ProjectId = "project" });

        Action act = validator.AssertSettings;

        act.Should().Throw<ConfigurationMessageBusException>()
            .WithMessage("*PathKind*");
    }

    [Fact]
    public void RequestResponse_RequiresSubscriptionName()
    {
        _settings.RequestResponse = new RequestResponseSettings
        {
            Path = "replies",
            PathKind = PathKind.Topic
        };
        var validator = new GooglePubSubMessageBusSettingsValidationService(
            _settings,
            new GooglePubSubMessageBusSettings { ProjectId = "project" });

        Action act = validator.AssertSettings;

        act.Should().Throw<ConfigurationMessageBusException>()
            .WithMessage("*SubscriptionName*");
    }

    [Fact]
    public void ValidTopicProducerAndConsumer_AreAccepted()
    {
        _settings.Producers.Add(new ProducerSettings
        {
            MessageType = typeof(TestMessage),
            DefaultPath = "orders",
            PathKind = PathKind.Topic
        });
        _ = new ConsumerBuilder<TestMessage>(_settings)
            .Topic("orders")
            .SubscriptionName("orders-worker")
            .WithConsumer<IConsumer<TestMessage>>();
        var validator = new GooglePubSubMessageBusSettingsValidationService(
            _settings,
            new GooglePubSubMessageBusSettings { ProjectId = "project" });

        Action act = validator.AssertSettings;
        act.Should().NotThrow();
    }
}
