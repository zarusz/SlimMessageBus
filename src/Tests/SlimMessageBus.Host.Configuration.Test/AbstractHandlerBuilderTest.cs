namespace SlimMessageBus.Host.Test.Config;

public class AbstractHandlerBuilderTest
{
    private readonly MessageBusSettings _messageBusSettings = new();
    private readonly string _path = "test-path";

    [Fact]
    public void When_TopicSet_Given_Topic_Then_PathShouldBeSet()
    {
        // arrange
        var subject = new HandlerBuilder<SomeRequest, SomeResponse>(_messageBusSettings);

        // act
        var result = subject.Topic(_path);

        // assert
        result.Should().BeSameAs(subject);
        subject.ConsumerSettings.Path.Should().Be(_path);
        subject.ConsumerSettings.PathKind.Should().Be(PathKind.Topic);
    }

    [Fact]
    public void When_TopicSet_Given_TopicWithConfig_Then_PathAndConfigShouldBeApplied()
    {
        // arrange
        var topicConfig = new Mock<Action<HandlerBuilder<SomeRequest, SomeResponse>>>();
        var subject = new HandlerBuilder<SomeRequest, SomeResponse>(_messageBusSettings);

        // act
        var result = subject.Topic(_path, topicConfig.Object);

        // assert
        result.Should().BeSameAs(subject);
        subject.ConsumerSettings.Path.Should().Be(_path);
        topicConfig.Verify(x => x(subject), Times.Once);
    }

    [Fact]
    public void When_InstancesSet_Given_Value_Then_InstancesShouldBeSet()
    {
        // arrange
        var instances = 5;
        var subject = new HandlerBuilder<SomeRequest, SomeResponse>(_messageBusSettings);

        // act
        var result = subject.Instances(instances);

        // assert
        result.Should().BeSameAs(subject);
        subject.ConsumerSettings.Instances.Should().Be(instances);
    }

    [Fact]
    public void When_WithHandlerType_Given_HandlerType_Then_ConsumerTypeShouldBeSet()
    {
        // arrange
        var handlerType = typeof(SomeRequestMessageHandler);
        var subject = new HandlerBuilder<SomeRequest, SomeResponse>(_messageBusSettings);

        // act
        var result = subject.WithHandler(handlerType);

        // assert
        result.Should().BeSameAs(subject);
        subject.ConsumerSettings.ConsumerType.Should().Be(handlerType);
        subject.ConsumerSettings.Invokers.Should().Contain(subject.ConsumerSettings);
    }

    [Fact]
    public void When_WithHandlerForDerivedType_Given_HandlerTypeAndDerivedType_Then_InvokerShouldBeAdded()
    {
        // arrange
        var handlerType = typeof(SomeDerivedRequestMessageHandler);
        var derivedType = typeof(SomeDerivedRequest);
        var subject = new HandlerBuilder<SomeRequest, SomeResponse>(_messageBusSettings);

        // act
        var result = subject.WithHandler(handlerType, derivedType);

        // assert
        result.Should().BeSameAs(subject);
        subject.ConsumerSettings.Invokers.Should().HaveCount(1);
        var invoker = subject.ConsumerSettings.Invokers.Single();
        invoker.MessageType.Should().Be(derivedType);
        invoker.ConsumerType.Should().Be(handlerType);
    }

    [Fact]
    public void When_WithHandlerForDerivedType_Given_NotCompatibleDerivedType_Then_ExceptionShouldBeThrown()
    {
        // arrange
        var handlerType = typeof(SomeDerivedRequestMessageHandler);
        var derivedType = typeof(SomeRequestWithoutResponse); // Not a derived type of SomeRequest
        var subject = new HandlerBuilder<SomeRequest, SomeResponse>(_messageBusSettings);

        // act
        var act = () => subject.WithHandler(handlerType, derivedType);

        // assert
        act.Should()
            .Throw<ConfigurationMessageBusException>()
            .WithMessage($"The (derived) message type {derivedType} is not assignable to message type {typeof(SomeRequest)}");
    }
}
