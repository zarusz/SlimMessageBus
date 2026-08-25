namespace SlimMessageBus.Host.GooglePubSub.Test;

public class GooglePubSubConsumerContextExtensionsTests
{
    [Fact]
    public void TransportMessageAndSubscriptionName_RoundTripThroughContext()
    {
        var context = new ConsumerContext();
        var message = new PubsubMessage { MessageId = "message-1" };

        context.SetTransportMessage(message);
        context.SetSubscriptionName("orders-worker");

        ((IConsumerContext)context).GetTransportMessage().Should().BeSameAs(message);
        ((IConsumerContext)context).GetSubscriptionName().Should().Be("orders-worker");
    }

    [Fact]
    public void ContextAccessors_RejectNullContext()
    {
        IConsumerContext context = null;

        Action getMessage = () => context.GetTransportMessage();
        Action getSubscription = () => context.GetSubscriptionName();

        getMessage.Should().Throw<ArgumentNullException>();
        getSubscription.Should().Throw<ArgumentNullException>();
    }
}
