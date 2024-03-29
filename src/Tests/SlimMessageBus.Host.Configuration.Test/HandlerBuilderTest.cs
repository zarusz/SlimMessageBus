namespace SlimMessageBus.Host.Test.Config;

public class HandlerBuilderTest
{
    private readonly MessageBusSettings messageBusSettings;

    public HandlerBuilderTest()
    {
        messageBusSettings = new MessageBusSettings();
    }

    [Fact]
    public void When_Created_Given_RequestAndResposeType_Then_MessageType_And_ResponseType_And_DefaultHandlerTypeSet_ProperlySet()
    {
        // arrange

        // act
        var subject = new HandlerBuilder<SomeRequest, SomeResponse>(messageBusSettings);

        // assert
        subject.ConsumerSettings.ConsumerMode.Should().Be(ConsumerMode.RequestResponse);
        subject.ConsumerSettings.ConsumerType.Should().BeNull();
        subject.ConsumerSettings.MessageType.Should().Be(typeof(SomeRequest));
        subject.ConsumerSettings.ResponseType.Should().Be(typeof(SomeResponse));
    }

    [Fact]
    public void When_PathSet_Given_Path_Then_Path_ProperlySet()
    {
        // arrange
        var path = "topic";
        var subject = new HandlerBuilder<SomeRequest, SomeResponse>(messageBusSettings);

        // act
        subject.Path(path);

        // assert
        subject.ConsumerSettings.Path.Should().Be(path);
        subject.ConsumerSettings.PathKind.Should().Be(PathKind.Topic);
    }

    [Fact]
    public void When_Configured_Given_RequestResponse_Then_ProperSettings()
    {
        // arrange
        var path = "topic";

        var consumerContextMock = new Mock<IConsumerContext>();
        consumerContextMock.SetupGet(x => x.CancellationToken).Returns(new CancellationToken());

        // act
        var subject = new HandlerBuilder<SomeRequest, SomeResponse>(messageBusSettings)
            .Topic(path)
            .Instances(3)
            .WithHandler<SomeRequestMessageHandler>();

        // assert
        subject.ConsumerSettings.MessageType.Should().Be(typeof(SomeRequest));
        subject.ConsumerSettings.Path.Should().Be(path);
        subject.ConsumerSettings.Instances.Should().Be(3);

        subject.ConsumerSettings.ConsumerType.Should().Be(typeof(SomeRequestMessageHandler));
        subject.ConsumerSettings.ConsumerMode.Should().Be(ConsumerMode.RequestResponse);

        subject.ConsumerSettings.ResponseType.Should().Be(typeof(SomeResponse));

        subject.ConsumerSettings.Invokers.Count.Should().Be(1);

        var consumerInvokerSettings = subject.ConsumerSettings.Invokers.Single(x => x.MessageType == typeof(SomeRequest));
        consumerInvokerSettings.MessageType.Should().Be(typeof(SomeRequest));
        consumerInvokerSettings.ConsumerType.Should().Be(typeof(SomeRequestMessageHandler));
        Func<Task> call = () => consumerInvokerSettings.ConsumerMethod(new SomeRequestMessageHandler(), new SomeRequest(), consumerContextMock.Object, consumerContextMock.Object.CancellationToken);
        call.Should().ThrowAsync<NotImplementedException>().WithMessage(nameof(SomeRequest));
    }

    [Fact]
    public void When_Configured_Given_RequestWithoutResponse_Then_ProperSettings()
    {
        // arrange
        var path = "topic";

        var consumerContextMock = new Mock<IConsumerContext>();
        consumerContextMock.SetupGet(x => x.CancellationToken).Returns(new CancellationToken());

        // act
        var subject = new HandlerBuilder<SomeRequestWithoutResponse>(messageBusSettings)
            .Topic(path)
            .Instances(3)
            .WithHandler<SomeRequestWithoutResponseHandler>();

        // assert
        subject.ConsumerSettings.MessageType.Should().Be(typeof(SomeRequestWithoutResponse));
        subject.ConsumerSettings.Path.Should().Be(path);
        subject.ConsumerSettings.Instances.Should().Be(3);

        subject.ConsumerSettings.ConsumerType.Should().Be(typeof(SomeRequestWithoutResponseHandler));
        subject.ConsumerSettings.ConsumerMode.Should().Be(ConsumerMode.RequestResponse);

        subject.ConsumerSettings.ResponseType.Should().BeNull();

        subject.ConsumerSettings.Invokers.Count.Should().Be(1);

        var consumerInvokerSettings = subject.ConsumerSettings.Invokers.Single(x => x.MessageType == typeof(SomeRequestWithoutResponse));
        consumerInvokerSettings.MessageType.Should().Be(typeof(SomeRequestWithoutResponse));
        consumerInvokerSettings.ConsumerType.Should().Be(typeof(SomeRequestWithoutResponseHandler));
        Func<Task> call = () => consumerInvokerSettings.ConsumerMethod(new SomeRequestWithoutResponseHandler(), new SomeRequestWithoutResponse(), consumerContextMock.Object, consumerContextMock.Object.CancellationToken);
        call.Should().ThrowAsync<NotImplementedException>().WithMessage(nameof(SomeRequestWithoutResponse));
    }
}