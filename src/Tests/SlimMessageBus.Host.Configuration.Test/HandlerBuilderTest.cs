namespace SlimMessageBus.Host.Test.Config;

public class HandlerBuilderTest
{
    private readonly Fixture _fixture;
    private readonly MessageBusSettings _messageBusSettings;
    private readonly string _path;

    public HandlerBuilderTest()
    {
        _fixture = new Fixture();
        _messageBusSettings = new MessageBusSettings();
        _path = _fixture.Create<string>();
    }

    [Fact]
    public void When_Created_Given_RequestAndResposeType_Then_MessageType_And_ResponseType_And_DefaultHandlerTypeSet_ProperlySet()
    {
        // act
        var subject = new HandlerBuilder<SomeRequest, SomeResponse>(_messageBusSettings);

        // assert
        subject.ConsumerSettings.MessageType.Should().Be(typeof(SomeRequest));
        subject.ConsumerSettings.ResponseType.Should().Be(typeof(SomeResponse));
        subject.ConsumerSettings.ConsumerMode.Should().Be(ConsumerMode.RequestResponse);
        subject.ConsumerSettings.ConsumerType.Should().BeNull();
        subject.ConsumerSettings.Invokers.Should().BeEmpty();
    }

    [Fact]
    public void When_Created_Given_RequestWithoutResposeType_Then_MessageType_And_DefaultHandlerTypeSet_ProperlySet()
    {
        // act
        var subject = new HandlerBuilder<SomeRequestWithoutResponse>(_messageBusSettings);

        // assert
        subject.ConsumerSettings.MessageType.Should().Be(typeof(SomeRequestWithoutResponse));
        subject.ConsumerSettings.ResponseType.Should().BeNull();
        subject.ConsumerSettings.ConsumerMode.Should().Be(ConsumerMode.RequestResponse);
        subject.ConsumerSettings.ConsumerType.Should().BeNull();
        subject.ConsumerSettings.Invokers.Should().BeEmpty();
    }

    [Fact]
    public void When_PathSet_Given_Path_Then_Path_ProperlySet()
    {
        // arrange
        var pathConfig = new Mock<Action<HandlerBuilder<SomeRequest, SomeResponse>>>();
        var subject = new HandlerBuilder<SomeRequest, SomeResponse>(_messageBusSettings);

        // act
        subject.Path(_path, pathConfig.Object);

        // assert
        subject.ConsumerSettings.Path.Should().Be(_path);
        subject.ConsumerSettings.PathKind.Should().Be(PathKind.Topic);
        pathConfig.Verify(x => x(subject), Times.Once);
    }

    [Fact]
    public void When_PathSet_Given_ThePathWasUsedBeforeOnAnotherHandler_Then_ExceptionIsRaised()
    {
        // arrange
        var otherHandlerBuilder = new HandlerBuilder<SomeRequest, SomeResponse>(_messageBusSettings).Path(_path);
        var subject = new HandlerBuilder<SomeRequest, SomeResponse>(_messageBusSettings);

        // act
        var act = () => subject.Path(_path);

        // assert
        act.Should()
            .Throw<ConfigurationMessageBusException>()
            .WithMessage($"Attempted to configure request handler for path '*' when one was already configured. There can only be one request handler for a given path.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_Configured_Given_RequestResponse_Then_ProperSettings(bool ofContext)
    {
        // arrange
        var consumerContextMock = new Mock<IConsumerContext>();
        consumerContextMock.SetupGet(x => x.CancellationToken).Returns(new CancellationToken());

        var consumerType = ofContext ? typeof(SomeRequestMessageHandlerOfContext) : typeof(SomeRequestMessageHandler);

        // act
        var subject = new HandlerBuilder<SomeRequest, SomeResponse>(_messageBusSettings)
            .Topic(_path)
            .Instances(3);

        if (ofContext)
        {
            subject.WithHandlerOfContext<SomeRequestMessageHandlerOfContext>();
            subject.WithHandlerOfContext<SomeDerivedRequestMessageHandlerOfContext, SomeDerivedRequest>();
        }
        else
        {
            subject.WithHandler<SomeRequestMessageHandler>();
            subject.WithHandler<SomeDerivedRequestMessageHandler, SomeDerivedRequest>();
        }

        // assert
        subject.ConsumerSettings.MessageType.Should().Be(typeof(SomeRequest));
        subject.ConsumerSettings.Path.Should().Be(_path);
        subject.ConsumerSettings.Instances.Should().Be(3);

        subject.ConsumerSettings.ConsumerType.Should().Be(consumerType);
        subject.ConsumerSettings.ConsumerMode.Should().Be(ConsumerMode.RequestResponse);

        subject.ConsumerSettings.ResponseType.Should().Be(typeof(SomeResponse));

        subject.ConsumerSettings.Invokers.Count.Should().Be(2);

        var consumerInvokerSettings = subject.ConsumerSettings.Invokers.Single(x => x.MessageType == typeof(SomeRequest));
        consumerInvokerSettings.Should().NotBeNull();
        consumerInvokerSettings.ConsumerType.Should().Be(consumerType);
        Func<Task> call = () => consumerInvokerSettings.ConsumerMethod(ofContext ? new SomeRequestMessageHandlerOfContext() : new SomeRequestMessageHandler(), new SomeRequest(), consumerContextMock.Object, consumerContextMock.Object.CancellationToken);
        call.Should().ThrowAsync<NotImplementedException>().WithMessage(nameof(SomeRequest));

        consumerInvokerSettings = subject.ConsumerSettings.Invokers.Single(x => x.MessageType == typeof(SomeDerivedRequest));
        consumerInvokerSettings.Should().NotBeNull();
        consumerInvokerSettings.ConsumerType.Should().Be(ofContext ? typeof(SomeDerivedRequestMessageHandlerOfContext) : typeof(SomeDerivedRequestMessageHandler));
        call = () => consumerInvokerSettings.ConsumerMethod(ofContext ? new SomeDerivedRequestMessageHandlerOfContext() : new SomeDerivedRequestMessageHandler(), new SomeDerivedRequest(), consumerContextMock.Object, consumerContextMock.Object.CancellationToken);
        call.Should().ThrowAsync<NotImplementedException>().WithMessage(nameof(SomeRequest));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void When_Configured_Given_RequestWithoutResponse_And_HandlersWithDerivedMessageType_Then_ProperSettings(bool ofContext)
    {
        // arrange
        var consumerContextMock = new Mock<IConsumerContext>();
        consumerContextMock.SetupGet(x => x.CancellationToken).Returns(new CancellationToken());

        var consumerType = ofContext ? typeof(SomeRequestWithoutResponseHandlerOfContext) : typeof(SomeRequestWithoutResponseHandler);

        // act
        var subject = new HandlerBuilder<SomeRequestWithoutResponse>(_messageBusSettings)
            .Topic(_path)
            .Instances(3);

        if (ofContext)
        {
            subject.WithHandlerOfContext<SomeRequestWithoutResponseHandlerOfContext>();
            subject.WithHandlerOfContext<SomeDerivedRequestWithoutResponseHandlerOfContext, SomeDerivedRequestWithoutResponse>();
        }
        else
        {
            subject.WithHandler<SomeRequestWithoutResponseHandler>();
            subject.WithHandler<SomeDerivedRequestWithoutResponseHandler, SomeDerivedRequestWithoutResponse>();
        }

        // assert
        subject.ConsumerSettings.MessageType.Should().Be(typeof(SomeRequestWithoutResponse));
        subject.ConsumerSettings.Path.Should().Be(_path);
        subject.ConsumerSettings.Instances.Should().Be(3);

        subject.ConsumerSettings.ConsumerType.Should().Be(consumerType);
        subject.ConsumerSettings.ConsumerMode.Should().Be(ConsumerMode.RequestResponse);

        subject.ConsumerSettings.ResponseType.Should().BeNull();

        subject.ConsumerSettings.Invokers.Count.Should().Be(2);

        var consumerInvokerSettings = subject.ConsumerSettings.Invokers.Single(x => x.MessageType == typeof(SomeRequestWithoutResponse));
        consumerInvokerSettings.ConsumerType.Should().Be(consumerType);
        Func<Task> call = () => consumerInvokerSettings.ConsumerMethod(ofContext ? new SomeRequestWithoutResponseHandlerOfContext() : new SomeRequestWithoutResponseHandler(), new SomeRequestWithoutResponse(), consumerContextMock.Object, consumerContextMock.Object.CancellationToken);
        call.Should().ThrowAsync<NotImplementedException>().WithMessage(nameof(SomeRequestWithoutResponse));

        consumerInvokerSettings = subject.ConsumerSettings.Invokers.Single(x => x.MessageType == typeof(SomeDerivedRequestWithoutResponse));
        consumerInvokerSettings.ConsumerType.Should().Be(ofContext ? typeof(SomeDerivedRequestWithoutResponseHandlerOfContext) : typeof(SomeDerivedRequestWithoutResponseHandler));
        call = () => consumerInvokerSettings.ConsumerMethod(ofContext ? new SomeDerivedRequestWithoutResponseHandlerOfContext() : new SomeDerivedRequestWithoutResponseHandler(), new SomeDerivedRequestWithoutResponse(), consumerContextMock.Object, consumerContextMock.Object.CancellationToken);
        call.Should().ThrowAsync<NotImplementedException>().WithMessage(nameof(SomeDerivedRequestWithoutResponse));
    }
}