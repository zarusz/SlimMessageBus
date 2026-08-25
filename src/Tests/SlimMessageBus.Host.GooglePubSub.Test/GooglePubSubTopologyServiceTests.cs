namespace SlimMessageBus.Host.GooglePubSub.Test;

using Grpc.Core;

public class GooglePubSubTopologyServiceTests
{
    private readonly Mock<PublisherServiceApiClient> _publisher = new();
    private readonly Mock<SubscriberServiceApiClient> _subscriber = new();
    private readonly GooglePubSubMessageBusSettings _providerSettings = new() { ProjectId = "project" };

    private GooglePubSubTopologyService CreateSubject() => new(
        NullLogger<GooglePubSubTopologyService>.Instance,
        new MessageBusSettings(),
        _providerSettings,
        _publisher.Object,
        _subscriber.Object);

    [Fact]
    public async Task EnsureTopic_CreatesMissingTopicAndAppliesOptions()
    {
        var topicName = TopicName.FromProjectTopic("project", "orders");
        _publisher.Setup(x => x.GetTopicAsync(topicName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(NotFound());
        _publisher.Setup(x => x.CreateTopicAsync(It.IsAny<Topic>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Topic topic, CancellationToken _) => topic);
        _providerSettings.TopologyProvisioning.CreateTopicOptions = topic => topic.Labels["global"] = "true";

        await CreateSubject().EnsureTopic(
            "orders",
            canCreate: true,
            [topic => topic.Labels["local"] = "true"],
            CancellationToken.None);

        _publisher.Verify(x => x.CreateTopicAsync(It.Is<Topic>(topic =>
            topic.TopicName == topicName &&
            topic.Labels["global"] == "true" &&
            topic.Labels["local"] == "true"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsureTopic_DoesNotCreateWhenDisabled()
    {
        var topicName = TopicName.FromProjectTopic("project", "orders");
        _publisher.Setup(x => x.GetTopicAsync(topicName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(NotFound());

        await CreateSubject().EnsureTopic("orders", canCreate: false, [], CancellationToken.None);

        _publisher.Verify(x => x.CreateTopicAsync(It.IsAny<Topic>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnsureTopic_DoesNotCreateExistingTopic()
    {
        var topicName = TopicName.FromProjectTopic("project", "orders");
        _publisher.Setup(x => x.GetTopicAsync(topicName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Topic { TopicName = topicName });

        await CreateSubject().EnsureTopic("orders", canCreate: true, [], CancellationToken.None);

        _publisher.Verify(x => x.CreateTopicAsync(It.IsAny<Topic>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnsureTopic_IgnoresConcurrentCreation()
    {
        var topicName = TopicName.FromProjectTopic("project", "orders");
        _publisher.Setup(x => x.GetTopicAsync(topicName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(NotFound());
        _publisher.Setup(x => x.CreateTopicAsync(It.IsAny<Topic>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(AlreadyExists());

        var act = () => CreateSubject().EnsureTopic("orders", canCreate: true, [], CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureSubscription_CreatesMissingPullSubscriptionAndAppliesOptions()
    {
        var subscriptionName = SubscriptionName.FromProjectSubscription("project", "orders-worker");
        _subscriber.Setup(x => x.GetSubscriptionAsync(subscriptionName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(NotFound());
        _subscriber.Setup(x => x.CreateSubscriptionAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription subscription, CancellationToken _) => subscription);

        await CreateSubject().EnsureSubscription(
            "orders",
            "orders-worker",
            [subscription => subscription.AckDeadlineSeconds = 60],
            CancellationToken.None);

        _subscriber.Verify(x => x.CreateSubscriptionAsync(It.Is<Subscription>(subscription =>
            subscription.SubscriptionName == subscriptionName &&
            subscription.Topic == "projects/project/topics/orders" &&
            subscription.AckDeadlineSeconds == 60), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsureSubscription_DoesNotCreateExistingMatchingSubscription()
    {
        var subscriptionName = SubscriptionName.FromProjectSubscription("project", "orders-worker");
        _subscriber.Setup(x => x.GetSubscriptionAsync(subscriptionName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Subscription
            {
                SubscriptionName = subscriptionName,
                TopicAsTopicName = TopicName.FromProjectTopic("project", "orders")
            });

        await CreateSubject().EnsureSubscription("orders", "orders-worker", [], CancellationToken.None);

        _subscriber.Verify(x => x.CreateSubscriptionAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnsureSubscription_RejectsExistingSubscriptionForDifferentTopic()
    {
        var subscriptionName = SubscriptionName.FromProjectSubscription("project", "orders-worker");
        _subscriber.Setup(x => x.GetSubscriptionAsync(subscriptionName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Subscription
            {
                SubscriptionName = subscriptionName,
                TopicAsTopicName = TopicName.FromProjectTopic("project", "other")
            });

        var act = () => CreateSubject().EnsureSubscription("orders", "orders-worker", [], CancellationToken.None);

        await act.Should().ThrowAsync<ConfigurationMessageBusException>()
            .WithMessage("*already belongs to topic*");
    }

    [Fact]
    public async Task EnsureSubscription_DoesNotCreateWhenDisabled()
    {
        var subscriptionName = SubscriptionName.FromProjectSubscription("project", "orders-worker");
        _subscriber.Setup(x => x.GetSubscriptionAsync(subscriptionName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(NotFound());
        _providerSettings.TopologyProvisioning.CanConsumerCreateSubscription = false;

        await CreateSubject().EnsureSubscription("orders", "orders-worker", [], CancellationToken.None);

        _subscriber.Verify(x => x.CreateSubscriptionAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnsureSubscription_IgnoresConcurrentCreation()
    {
        var subscriptionName = SubscriptionName.FromProjectSubscription("project", "orders-worker");
        _subscriber.Setup(x => x.GetSubscriptionAsync(subscriptionName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(NotFound());
        _subscriber.Setup(x => x.CreateSubscriptionAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(AlreadyExists());

        var act = () => CreateSubject().EnsureSubscription("orders", "orders-worker", [], CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureSubscription_RejectsNonPullSubscription()
    {
        var subscriptionName = SubscriptionName.FromProjectSubscription("project", "orders-worker");
        _subscriber.Setup(x => x.GetSubscriptionAsync(subscriptionName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(NotFound());

        var act = () => CreateSubject().EnsureSubscription(
            "orders",
            "orders-worker",
            [subscription => subscription.PushConfig = new PushConfig { PushEndpoint = "https://example.test/messages" }],
            CancellationToken.None);

        await act.Should().ThrowAsync<ConfigurationMessageBusException>()
            .WithMessage("*pull subscription*");
    }

    private static RpcException NotFound()
        => new(new Status(StatusCode.NotFound, "not found"));

    private static RpcException AlreadyExists()
        => new(new Status(StatusCode.AlreadyExists, "already exists"));
}
