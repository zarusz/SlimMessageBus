namespace SlimMessageBus.Host.AzureServiceBus.Test;

using Azure.Messaging.ServiceBus;

using SlimMessageBus.Host;

public class ServiceBusMessageBusSettingsTests
{
    private readonly ServiceBusMessageBusSettings _subject = new();

    [Fact]
    public void When_SetSubscriptionName_Given_SubscriptionNameIsNull_Then_ThrowsException()
    {
        // arrange

        // act
        var act = () => _subject.SubscriptionName(null);

        // assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void When_GetSubscriptionName_Given_SubscriptionNameAtTheBusLevelOrConsumerLevel_Then_DefaultToBusLevelOrTakesConsumerValue(bool consumerSubscriptionNameApplied)
    {
        // arrange
        var busSubscriptionName = "global-sub-name";
        _subject.SubscriptionName(busSubscriptionName);

        var consumerBuilder = new ConsumerBuilder<SomeMessage>(new MessageBusSettings());

        var consumerSubscriptionName = "consumer-sub-name";
        if (consumerSubscriptionNameApplied)
        {
            consumerBuilder.SubscriptionName(consumerSubscriptionName);
        }

        // act
        var subscriptionName = consumerBuilder.ConsumerSettings.GetSubscriptionName(_subject);

        // assert
        subscriptionName.Should().Be(consumerSubscriptionNameApplied ? consumerSubscriptionName : busSubscriptionName);
    }

    [Fact]
    public void When_WithModifier_Given_NullValue_Then_ThrowsException()
    {
        // arrange

        // act
        var act = () => _subject.WithModifier(null);

        // assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void When_WithModifier_Given_SeveralModifiers_Then_ExecutesThemAll(bool executePrevious)
    {
        // arrange
        var someMessage = new SomeMessage();
        var transportMessage = new Mock<ServiceBusMessage>();

        var modifier1 = new Mock<AsbMessageModifier<object>>();
        var modifier2 = new Mock<AsbMessageModifier<object>>();
        var modifier3 = new Mock<AsbMessageModifier<OtherMessage>>();
        var modifier4 = new Mock<AsbMessageModifier<SomeMessage>>();

        _subject.WithModifier(modifier1.Object, executePrevious: executePrevious);
        _subject.WithModifier(modifier2.Object, executePrevious: executePrevious);
        _subject.WithModifier(modifier3.Object, executePrevious: executePrevious);
        _subject.WithModifier(modifier4.Object, executePrevious: executePrevious);

        var modifier = _subject.GetMessageModifier();

        // act
        modifier(someMessage, transportMessage.Object);

        // assert
        if (executePrevious)
        {
            modifier1.Verify(x => x(someMessage, transportMessage.Object), Times.Once);
            modifier2.Verify(x => x(someMessage, transportMessage.Object), Times.Once);
        }
        modifier3.Verify(x => x(It.IsAny<OtherMessage>(), transportMessage.Object), Times.Never);
        modifier4.Verify(x => x(someMessage, transportMessage.Object), Times.Once);
    }
}