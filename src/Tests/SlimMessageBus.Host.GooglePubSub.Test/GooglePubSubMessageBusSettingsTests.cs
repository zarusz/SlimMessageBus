namespace SlimMessageBus.Host.GooglePubSub.Test;

using Google.Apis.Auth.OAuth2;

public class GooglePubSubMessageBusSettingsTests
{
    private record TestMessage(string Id);

    [Fact]
    public void WithModifier_ComposesGlobalModifiers()
    {
        var settings = new GooglePubSubMessageBusSettings();
        settings.WithModifier((_, message) => message.Attributes["first"] = "1");
        settings.WithModifier<TestMessage>((message, transportMessage) => transportMessage.OrderingKey = message.Id);
        var modifier = settings.GetOrDefault(GooglePubSubProperties.MessageModifier);
        var transportMessage = new PubsubMessage();

        modifier(new TestMessage("customer-1"), transportMessage);

        transportMessage.Attributes["first"].Should().Be("1");
        transportMessage.OrderingKey.Should().Be("customer-1");
    }

    [Fact]
    public void WithModifier_CanReplacePreviousModifierAndIgnoresOtherTypes()
    {
        var settings = new GooglePubSubMessageBusSettings();
        settings.WithModifier((_, message) => message.Attributes["previous"] = "true");
        settings.WithModifier<TestMessage>((message, transportMessage) => transportMessage.OrderingKey = message.Id, executePrevious: false);
        var modifier = settings.GetOrDefault(GooglePubSubProperties.MessageModifier);
        var transportMessage = new PubsubMessage();

        modifier("not-a-test-message", transportMessage);

        transportMessage.Attributes.Should().BeEmpty();
        transportMessage.OrderingKey.Should().BeEmpty();
    }

    [Fact]
    public void SettingsMethods_RejectNullArguments()
    {
        var settings = new GooglePubSubMessageBusSettings();

        Action nullModifier = () => settings.WithModifier((GooglePubSubMessageModifier<object>)null);
        Action nullTypedModifier = () => settings.WithModifier<TestMessage>(null);
        Action nullCredentialsPath = () => settings.UseCredentialsPath(null);
        Action nullCredential = () => settings.UseGoogleCredential(null);

        nullModifier.Should().Throw<ArgumentNullException>();
        nullTypedModifier.Should().Throw<ArgumentNullException>();
        nullCredentialsPath.Should().Throw<ArgumentNullException>();
        nullCredential.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UseGoogleCredential_ReplacesAllClientFactories()
    {
        var settings = new GooglePubSubMessageBusSettings();
        var publisherFactory = settings.PublisherClientFactory;
        var subscriberFactory = settings.SubscriberClientFactory;
        var publisherAdminFactory = settings.PublisherServiceApiClientFactory;
        var subscriberAdminFactory = settings.SubscriberServiceApiClientFactory;

        var result = settings.UseGoogleCredential(GoogleCredential.FromAccessToken("test-token"));

        result.Should().BeSameAs(settings);
        settings.PublisherClientFactory.Should().NotBeSameAs(publisherFactory);
        settings.SubscriberClientFactory.Should().NotBeSameAs(subscriberFactory);
        settings.PublisherServiceApiClientFactory.Should().NotBeSameAs(publisherAdminFactory);
        settings.SubscriberServiceApiClientFactory.Should().NotBeSameAs(subscriberAdminFactory);
    }
}
