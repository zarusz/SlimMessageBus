namespace SlimMessageBus.Host.GooglePubSub.Test;

public class GooglePubSubConfigurationTests
{
    private record TestMessage(string Id);

    [Fact]
    public void TopologyProvisioning_IsEnabledByDefault()
    {
        var settings = new GooglePubSubMessageBusSettings();

        settings.TopologyProvisioning.Enabled.Should().BeTrue();
        settings.TopologyProvisioning.CanProducerCreateTopic.Should().BeTrue();
        settings.TopologyProvisioning.CanConsumerCreateTopic.Should().BeTrue();
        settings.TopologyProvisioning.CanConsumerCreateSubscription.Should().BeTrue();
    }

    [Fact]
    public void SubscriptionName_SetsConsumerProperty()
    {
        var settings = new MessageBusSettings();
        var builder = new ConsumerBuilder<TestMessage>(settings);

        var result = builder.SubscriptionName("orders-worker");

        result.Should().BeSameAs(builder);
        builder.ConsumerSettings.GetSubscriptionName().Should().Be("orders-worker");
    }

    [Fact]
    public void ConsumerTopologyOptions_SetConsumerProperties()
    {
        var builder = new ConsumerBuilder<TestMessage>(new MessageBusSettings());
        Action<Topic> topicOptions = topic => topic.Labels["source"] = "consumer";
        Action<Subscription> subscriptionOptions = subscription => subscription.AckDeadlineSeconds = 30;

        var result = builder
            .CreateTopicOptions(topicOptions)
            .CreateSubscriptionOptions(subscriptionOptions);

        result.Should().BeSameAs(builder);
        builder.ConsumerSettings.GetOrDefault(GooglePubSubProperties.CreateTopicOptions).Should().BeSameAs(topicOptions);
        builder.ConsumerSettings.GetOrDefault(GooglePubSubProperties.CreateSubscriptionOptions).Should().BeSameAs(subscriptionOptions);
    }

    [Fact]
    public void WithModifier_SetsTypedProducerModifier()
    {
        var producerSettings = new ProducerSettings { MessageType = typeof(TestMessage) };
        var builder = new ProducerBuilder<TestMessage>(producerSettings);
        builder.WithModifier((message, transportMessage) => transportMessage.OrderingKey = message.Id);

        var modifier = producerSettings.GetOrDefault(GooglePubSubProperties.MessageModifier);
        var transportMessage = new PubsubMessage();
        modifier(new TestMessage("customer-1"), transportMessage);

        transportMessage.OrderingKey.Should().Be("customer-1");
    }

    [Fact]
    public void ProducerCreateTopicOptions_SetsProducerProperty()
    {
        var builder = new ProducerBuilder<TestMessage>(new ProducerSettings { MessageType = typeof(TestMessage) });
        Action<Topic> topicOptions = topic => topic.Labels["source"] = "producer";

        var result = builder.CreateTopicOptions(topicOptions);

        result.Should().BeSameAs(builder);
        builder.Settings.GetOrDefault(GooglePubSubProperties.CreateTopicOptions).Should().BeSameAs(topicOptions);
    }

    [Fact]
    public void BuilderExtensions_RejectNullArguments()
    {
        var consumerBuilder = new ConsumerBuilder<TestMessage>(new MessageBusSettings());
        var producerBuilder = new ProducerBuilder<TestMessage>(new ProducerSettings { MessageType = typeof(TestMessage) });

        Action nullConsumerBuilder = () => GooglePubSubConsumerBuilderExtensions.SubscriptionName<ConsumerBuilder<TestMessage>>(null, "subscription");
        Action nullSubscriptionName = () => consumerBuilder.SubscriptionName(null);
        Action nullConsumerTopicOptions = () => consumerBuilder.CreateTopicOptions(null);
        Action nullConsumerSubscriptionOptions = () => consumerBuilder.CreateSubscriptionOptions(null);
        Action nullProducerBuilder = () => GooglePubSubProducerBuilderExtensions.WithModifier<TestMessage>(null, (_, _) => { });
        Action nullProducerModifier = () => producerBuilder.WithModifier(null);
        Action nullProducerTopicOptions = () => producerBuilder.CreateTopicOptions(null);
        Action nullMessageBusBuilder = () => MessageBusBuilderExtensions.WithProviderGooglePubSub(null, _ => { });
        Action nullProviderConfiguration = () => MessageBusBuilder.Create().WithProviderGooglePubSub(null);

        nullConsumerBuilder.Should().Throw<ArgumentNullException>();
        nullSubscriptionName.Should().Throw<ArgumentNullException>();
        nullConsumerTopicOptions.Should().Throw<ArgumentNullException>();
        nullConsumerSubscriptionOptions.Should().Throw<ArgumentNullException>();
        nullProducerBuilder.Should().Throw<ArgumentNullException>();
        nullProducerModifier.Should().Throw<ArgumentNullException>();
        nullProducerTopicOptions.Should().Throw<ArgumentNullException>();
        nullMessageBusBuilder.Should().Throw<ArgumentNullException>();
        nullProviderConfiguration.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ResourceNames_AcceptIdsAndFullyQualifiedNames()
    {
        GooglePubSubResourceName.ResolveTopic("project-a", "orders").ToString()
            .Should().Be("projects/project-a/topics/orders");
        GooglePubSubResourceName.ResolveTopic("ignored", "projects/project-b/topics/orders").ToString()
            .Should().Be("projects/project-b/topics/orders");
        GooglePubSubResourceName.ResolveSubscription("project-a", "orders-worker").ToString()
            .Should().Be("projects/project-a/subscriptions/orders-worker");
    }
}
