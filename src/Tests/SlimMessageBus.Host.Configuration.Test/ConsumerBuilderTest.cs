namespace SlimMessageBus.Host.Test.Config;

public class ConsumerBuilderTest
{
    private readonly MessageBusSettings _messageBusSettings;
    private readonly string _path;

    public ConsumerBuilderTest()
    {
        _messageBusSettings = new MessageBusSettings();
        _path = "topic";
    }

    [Fact]
    public void Given_MessageType_When_Configured_Then_MessageType_ProperlySet_And_ConsumerTypeNull()
    {
        // arrange

        // act
        var subject = new ConsumerBuilder<SomeMessage>(_messageBusSettings);

        // assert
        subject.ConsumerSettings.ConsumerMode.Should().Be(ConsumerMode.Consumer);
        subject.ConsumerSettings.ConsumerType.Should().BeNull();
        subject.ConsumerSettings.MessageType.Should().Be(typeof(SomeMessage));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Given_Path_Set_When_Configured_Then_Path_ProperlySet(bool delegatePassed)
    {
        // arrange
        var pathDelegate = new Mock<Action<ConsumerBuilder<SomeMessage>>>();

        // act
        var subject = new ConsumerBuilder<SomeMessage>(_messageBusSettings).Path(_path, delegatePassed ? pathDelegate.Object : null);

        // assert
        subject.ConsumerSettings.Path.Should().Be(_path);
        subject.ConsumerSettings.PathKind.Should().Be(PathKind.Topic);
        if (delegatePassed)
        {
            pathDelegate.Verify(x => x.Invoke(subject), Times.Once);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Given_Topic_Set_When_Configured_Then_Topic_ProperlySet(bool delegatePassed)
    {
        // arrange
        var pathDelegate = new Mock<Action<ConsumerBuilder<SomeMessage>>>();

        // act
        var subject = new ConsumerBuilder<SomeMessage>(_messageBusSettings).Topic(_path, delegatePassed ? pathDelegate.Object : null);

        // assert
        subject.ConsumerSettings.Path.Should().Be(_path);
        subject.ConsumerSettings.PathKind.Should().Be(PathKind.Topic);
        if (delegatePassed)
        {
            pathDelegate.Verify(x => x.Invoke(subject), Times.Once);
        }
    }

    [Fact]
    public void Given_Instances_Set_When_Configured_Then_Instances_ProperlySet()
    {
        // act
        var subject = new ConsumerBuilder<SomeMessage>(_messageBusSettings)
            .Instances(3);

        // assert
        subject.ConsumerSettings.Instances.Should().Be(3);
    }

    [Fact]
    public void Given_BaseMessageType_And_ItsHierarchy_When_WithConsumer_ForTheBaseTypeAndDerivedTypes_Then_TheConsumerSettingsAreCorrect()
    {
        // arrange
        var consumerContextMock = new Mock<IConsumerContext>();
        consumerContextMock.SetupGet(x => x.CancellationToken).Returns(new CancellationToken());

        // act
        var subject = new ConsumerBuilder<BaseMessage>(_messageBusSettings)
            .Topic(_path)
            .WithConsumer<BaseMessageConsumer>()
            .WithConsumer<DerivedAMessageConsumer, DerivedAMessage>()
            .WithConsumer<DerivedBMessageConsumer, DerivedBMessage>()
            .WithConsumer<Derived2AMessageConsumer, Derived2AMessage>();

        // assert
        subject.ConsumerSettings.ResponseType.Should().BeNull();

        subject.ConsumerSettings.ConsumerMode.Should().Be(ConsumerMode.Consumer);
        subject.ConsumerSettings.ConsumerType.Should().Be(typeof(BaseMessageConsumer));
        Func<Task> call = () => subject.ConsumerSettings.ConsumerMethod(new BaseMessageConsumer(), new BaseMessage(), consumerContextMock.Object, consumerContextMock.Object.CancellationToken);
        call.Should().ThrowAsync<NotImplementedException>().WithMessage(nameof(BaseMessage));

        subject.ConsumerSettings.Invokers.Count.Should().Be(4);

        var consumerInvokerSettings = subject.ConsumerSettings.Invokers.Single(x => x.MessageType == typeof(BaseMessage));
        consumerInvokerSettings.MessageType.Should().Be(typeof(BaseMessage));
        consumerInvokerSettings.ConsumerType.Should().Be(typeof(BaseMessageConsumer));
        consumerInvokerSettings.ParentSettings.Should().BeSameAs(subject.ConsumerSettings);
        call = () => consumerInvokerSettings.ConsumerMethod(new BaseMessageConsumer(), new BaseMessage(), consumerContextMock.Object, consumerContextMock.Object.CancellationToken);
        call.Should().ThrowAsync<NotImplementedException>().WithMessage(nameof(BaseMessage));

        consumerInvokerSettings = subject.ConsumerSettings.Invokers.Single(x => x.MessageType == typeof(DerivedAMessage));
        consumerInvokerSettings.MessageType.Should().Be(typeof(DerivedAMessage));
        consumerInvokerSettings.ConsumerType.Should().Be(typeof(DerivedAMessageConsumer));
        consumerInvokerSettings.ParentSettings.Should().BeSameAs(subject.ConsumerSettings);
        call = () => consumerInvokerSettings.ConsumerMethod(new DerivedAMessageConsumer(), new DerivedAMessage(), consumerContextMock.Object, consumerContextMock.Object.CancellationToken);
        call.Should().ThrowAsync<NotImplementedException>().WithMessage(nameof(DerivedAMessage));

        consumerInvokerSettings = subject.ConsumerSettings.Invokers.Single(x => x.MessageType == typeof(DerivedBMessage));
        consumerInvokerSettings.MessageType.Should().Be(typeof(DerivedBMessage));
        consumerInvokerSettings.ConsumerType.Should().Be(typeof(DerivedBMessageConsumer));
        consumerInvokerSettings.ParentSettings.Should().BeSameAs(subject.ConsumerSettings);
        call = () => consumerInvokerSettings.ConsumerMethod(new DerivedBMessageConsumer(), new DerivedBMessage(), consumerContextMock.Object, consumerContextMock.Object.CancellationToken);
        call.Should().ThrowAsync<NotImplementedException>().WithMessage(nameof(DerivedBMessage));

        consumerInvokerSettings = subject.ConsumerSettings.Invokers.Single(x => x.MessageType == typeof(Derived2AMessage));
        consumerInvokerSettings.MessageType.Should().Be(typeof(Derived2AMessage));
        consumerInvokerSettings.ConsumerType.Should().Be(typeof(Derived2AMessageConsumer));
        consumerInvokerSettings.ParentSettings.Should().BeSameAs(subject.ConsumerSettings);
        call = () => consumerInvokerSettings.ConsumerMethod(new Derived2AMessageConsumer(), new Derived2AMessage(), consumerContextMock.Object, consumerContextMock.Object.CancellationToken);
        call.Should().ThrowAsync<NotImplementedException>().WithMessage(nameof(Derived2AMessage));
    }

    [Fact]
    public void Given_BaseMessageType_And_ItsHierarchy_And_ConsumerOfContext_When_WithConsumer_ForTheBaseTypeAndDerivedTypes_Then_TheConsumerSettingsAreCorrect()
    {
        // arrange
        var consumerContextMock = new Mock<IConsumerContext>();
        consumerContextMock.SetupGet(x => x.CancellationToken).Returns(new CancellationToken());

        // act
        var subject = new ConsumerBuilder<BaseMessage>(_messageBusSettings)
            .Topic(_path)
            .WithConsumerOfContext<BaseMessageConsumerOfContext>()
            .WithConsumerOfContext<DerivedAMessageConsumerOfContext, DerivedAMessage>()
            .WithConsumerOfContext<DerivedBMessageConsumerOfContext, DerivedBMessage>()
            .WithConsumerOfContext<Derived2AMessageConsumerOfContext, Derived2AMessage>();

        // assert
        subject.ConsumerSettings.ResponseType.Should().BeNull();

        subject.ConsumerSettings.ConsumerMode.Should().Be(ConsumerMode.Consumer);
        subject.ConsumerSettings.ConsumerType.Should().Be(typeof(BaseMessageConsumerOfContext));
        Func<Task> call = () => subject.ConsumerSettings.ConsumerMethod(new BaseMessageConsumerOfContext(), new BaseMessage(), consumerContextMock.Object, consumerContextMock.Object.CancellationToken);
        call.Should().ThrowAsync<NotImplementedException>().WithMessage(nameof(BaseMessage));

        subject.ConsumerSettings.Invokers.Count.Should().Be(4);

        var consumerInvokerSettings = subject.ConsumerSettings.Invokers.Single(x => x.MessageType == typeof(BaseMessage));
        consumerInvokerSettings.MessageType.Should().Be(typeof(BaseMessage));
        consumerInvokerSettings.ConsumerType.Should().Be(typeof(BaseMessageConsumerOfContext));
        consumerInvokerSettings.ParentSettings.Should().BeSameAs(subject.ConsumerSettings);
        call = () => consumerInvokerSettings.ConsumerMethod(new BaseMessageConsumerOfContext(), new BaseMessage(), consumerContextMock.Object, consumerContextMock.Object.CancellationToken);
        call.Should().ThrowAsync<NotImplementedException>().WithMessage(nameof(BaseMessage));

        consumerInvokerSettings = subject.ConsumerSettings.Invokers.Single(x => x.MessageType == typeof(DerivedAMessage));
        consumerInvokerSettings.MessageType.Should().Be(typeof(DerivedAMessage));
        consumerInvokerSettings.ConsumerType.Should().Be(typeof(DerivedAMessageConsumerOfContext));
        consumerInvokerSettings.ParentSettings.Should().BeSameAs(subject.ConsumerSettings);
        call = () => consumerInvokerSettings.ConsumerMethod(new DerivedAMessageConsumerOfContext(), new DerivedAMessage(), consumerContextMock.Object, consumerContextMock.Object.CancellationToken);
        call.Should().ThrowAsync<NotImplementedException>().WithMessage(nameof(DerivedAMessage));

        consumerInvokerSettings = subject.ConsumerSettings.Invokers.Single(x => x.MessageType == typeof(DerivedBMessage));
        consumerInvokerSettings.MessageType.Should().Be(typeof(DerivedBMessage));
        consumerInvokerSettings.ConsumerType.Should().Be(typeof(DerivedBMessageConsumerOfContext));
        consumerInvokerSettings.ParentSettings.Should().BeSameAs(subject.ConsumerSettings);
        call = () => consumerInvokerSettings.ConsumerMethod(new DerivedBMessageConsumerOfContext(), new DerivedBMessage(), consumerContextMock.Object, consumerContextMock.Object.CancellationToken);
        call.Should().ThrowAsync<NotImplementedException>().WithMessage(nameof(DerivedBMessage));

        consumerInvokerSettings = subject.ConsumerSettings.Invokers.Single(x => x.MessageType == typeof(Derived2AMessage));
        consumerInvokerSettings.MessageType.Should().Be(typeof(Derived2AMessage));
        consumerInvokerSettings.ConsumerType.Should().Be(typeof(Derived2AMessageConsumerOfContext));
        consumerInvokerSettings.ParentSettings.Should().BeSameAs(subject.ConsumerSettings);
        call = () => consumerInvokerSettings.ConsumerMethod(new Derived2AMessageConsumerOfContext(), new Derived2AMessage(), consumerContextMock.Object, consumerContextMock.Object.CancellationToken);
        call.Should().ThrowAsync<NotImplementedException>().WithMessage(nameof(Derived2AMessage));
    }

    [Fact]
    public void Given_BaseRequestType_And_ItsHierarchy_When_WithConsumer_ForTheBaseTypeAndDerivedTypes_Then_TheConsumerSettingsAreCorrect()
    {
        // arrange
        var consumerContextMock = new Mock<IConsumerContext>();
        consumerContextMock.SetupGet(x => x.CancellationToken).Returns(new CancellationToken());

        // act
        var subject = new ConsumerBuilder<BaseRequest>(_messageBusSettings)
            .Topic(_path)
            .WithConsumer<BaseRequestConsumer>()
            .WithConsumer<DerivedRequestConsumer, DerivedRequest>();

        // assert
        subject.ConsumerSettings.ResponseType.Should().Be(typeof(BaseResponse));

        subject.ConsumerSettings.ConsumerMode.Should().Be(ConsumerMode.Consumer);
        subject.ConsumerSettings.ConsumerType.Should().Be(typeof(BaseRequestConsumer));
        Func<Task> call = () => subject.ConsumerSettings.ConsumerMethod(new BaseRequestConsumer(), new BaseRequest(), consumerContextMock.Object, consumerContextMock.Object.CancellationToken);
        call.Should().ThrowAsync<NotImplementedException>().WithMessage(nameof(BaseRequest));

        subject.ConsumerSettings.Invokers.Count.Should().Be(2);

        var consumerInvokerSettings = subject.ConsumerSettings.Invokers.Single(x => x.MessageType == typeof(BaseRequest));
        consumerInvokerSettings.MessageType.Should().Be(typeof(BaseRequest));
        consumerInvokerSettings.ConsumerType.Should().Be(typeof(BaseRequestConsumer));
        consumerInvokerSettings.ParentSettings.Should().BeSameAs(subject.ConsumerSettings);
        call = () => consumerInvokerSettings.ConsumerMethod(new BaseRequestConsumer(), new BaseRequest(), consumerContextMock.Object, consumerContextMock.Object.CancellationToken);
        call.Should().ThrowAsync<NotImplementedException>().WithMessage(nameof(BaseRequest));

        consumerInvokerSettings = subject.ConsumerSettings.Invokers.Single(x => x.MessageType == typeof(DerivedRequest));
        consumerInvokerSettings.MessageType.Should().Be(typeof(DerivedRequest));
        consumerInvokerSettings.ConsumerType.Should().Be(typeof(DerivedRequestConsumer));
        consumerInvokerSettings.ParentSettings.Should().BeSameAs(subject.ConsumerSettings);
        call = () => consumerInvokerSettings.ConsumerMethod(new DerivedRequestConsumer(), new DerivedRequest(), consumerContextMock.Object, consumerContextMock.Object.CancellationToken);
        call.Should().ThrowAsync<NotImplementedException>().WithMessage(nameof(DerivedRequest));
    }

    [Fact]
    public void When_WithConsumer_Given_CustomDelegateOverloadUsed_Then_ConsumerMethodSet()
    {
        // arrange
        var message = new SomeMessage();
        var consumerMock = new Mock<IConsumer<SomeMessage>>();
        var subject = new ConsumerBuilder<SomeMessage>(_messageBusSettings);
        var ct = new CancellationToken();

        // act
        subject.WithConsumer<IConsumer<SomeMessage>>((c, m, context, ct) => c.OnHandle(m, ct));

        // assert
        subject.ConsumerSettings.Invokers.Count.Should().Be(1);
        subject.ConsumerSettings.ConsumerMethod(consumerMock.Object, message, null, ct);

        consumerMock.Verify(x => x.OnHandle(message, ct), Times.Once);
    }

    public class BaseMessage
    {
    }

    public class DerivedAMessage : BaseMessage
    {
    }

    public class DerivedBMessage : BaseMessage
    {
    }

    public class Derived2AMessage : DerivedAMessage
    {
    }

    public class BaseMessageConsumer : IConsumer<BaseMessage>
    {
        public Task OnHandle(BaseMessage message, CancellationToken cancellationToken) => throw new NotImplementedException(nameof(BaseMessage));
    }

    public class DerivedAMessageConsumer : IConsumer<DerivedAMessage>
    {
        public Task OnHandle(DerivedAMessage message, CancellationToken cancellationToken) => throw new NotImplementedException(nameof(DerivedAMessage));
    }

    public class DerivedBMessageConsumer : IConsumer<DerivedBMessage>
    {
        public Task OnHandle(DerivedBMessage message, CancellationToken cancellationToken) => throw new NotImplementedException(nameof(DerivedBMessage));
    }

    public class Derived2AMessageConsumer : IConsumer<Derived2AMessage>
    {
        public Task OnHandle(Derived2AMessage message, CancellationToken cancellationToken) => throw new NotImplementedException(nameof(Derived2AMessage));
    }

    public class BaseMessageConsumerOfContext : IConsumer<IConsumerContext<BaseMessage>>
    {
        public Task OnHandle(IConsumerContext<BaseMessage> message, CancellationToken cancellationToken) => throw new NotImplementedException(nameof(BaseMessage));
    }

    public class DerivedAMessageConsumerOfContext : IConsumer<IConsumerContext<DerivedAMessage>>
    {
        public Task OnHandle(IConsumerContext<DerivedAMessage> message, CancellationToken cancellationToken) => throw new NotImplementedException(nameof(DerivedAMessage));
    }

    public class DerivedBMessageConsumerOfContext : IConsumer<IConsumerContext<DerivedBMessage>>
    {
        public Task OnHandle(IConsumerContext<DerivedBMessage> message, CancellationToken cancellationToken) => throw new NotImplementedException(nameof(DerivedBMessage));
    }

    public class Derived2AMessageConsumerOfContext : IConsumer<IConsumerContext<Derived2AMessage>>
    {
        public Task OnHandle(IConsumerContext<Derived2AMessage> message, CancellationToken cancellationToken) => throw new NotImplementedException(nameof(Derived2AMessage));
    }

    public class BaseResponse
    {
    }

    public class BaseRequest : IRequest<BaseResponse>
    {
    }

    public class DerivedRequest : BaseRequest
    {
    }

    public class BaseRequestConsumer : IConsumer<BaseRequest>
    {
        public Task OnHandle(BaseRequest message, CancellationToken cancellationToken) => throw new NotImplementedException(nameof(BaseRequest));
    }

    public class DerivedRequestConsumer : IConsumer<DerivedRequest>
    {
        public Task OnHandle(DerivedRequest message, CancellationToken cancellationToken) => throw new NotImplementedException(nameof(DerivedRequest));
    }
}