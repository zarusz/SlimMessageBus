namespace SlimMessageBus.Host.Configuration.Test;

using SlimMessageBus.Host.Test;

public class MessageConsumerContextTest
{
    [Fact]
    public void When_Constructor_Then_PropertiesUseTargetConsumerContext_And_ProvidedMessage_Given_MessageAndAnotherConsumerContext()
    {
        // arrange
        var message = new Mock<SomeMessage>();
        var consumer = new Mock<IConsumer<SomeMessage>>();
        var headers = new Dictionary<string, object>();
        var properties = new Dictionary<string, object>();
        var path = "path";
        var bus = Mock.Of<IMessageBus>();
        var ct = new CancellationToken();

        var untypedConsumerContext = new Mock<IConsumerContext>();
        untypedConsumerContext.SetupGet(x => x.Consumer).Returns(consumer.Object);
        untypedConsumerContext.SetupGet(x => x.Headers).Returns(headers);
        untypedConsumerContext.SetupGet(x => x.Properties).Returns(properties);
        untypedConsumerContext.SetupGet(x => x.Path).Returns(path);
        untypedConsumerContext.SetupGet(x => x.Bus).Returns(bus);
        untypedConsumerContext.SetupGet(x => x.CancellationToken).Returns(ct);

        // act
        var subject = new MessageConsumerContext<SomeMessage>(untypedConsumerContext.Object, message.Object);

        // assert
        subject.Message.Should().BeSameAs(message.Object);
        subject.Consumer.Should().BeSameAs(consumer.Object);
        subject.Headers.Should().BeSameAs(headers);
        subject.Properties.Should().BeSameAs(properties);
        subject.Path.Should().Be(path);
        subject.Bus.Should().BeSameAs(bus);
        subject.CancellationToken.Should().Be(ct);
    }
}
