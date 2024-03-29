namespace SlimMessageBus.Host.Test.Config;

public class ConsumerBuilderTest
{
    private readonly MessageBusSettings messageBusSettings;

    public ConsumerBuilderTest()
    {
        messageBusSettings = new MessageBusSettings();
    }

    [Fact]
    public void Given_MessageType_When_Configured_Then_MessageType_ProperlySet_And_ConsumerTypeNull()
    {
        // arrange

        // act
        var subject = new ConsumerBuilder<SomeMessage>(messageBusSettings);

        // assert
        subject.ConsumerSettings.ConsumerMode.Should().Be(ConsumerMode.Consumer);
        subject.ConsumerSettings.ConsumerType.Should().BeNull();
        subject.ConsumerSettings.MessageType.Should().Be(typeof(SomeMessage));
    }

    [Fact]
    public void Given_Path_Set_When_Configured_Then_Path_ProperlySet()
    {
        // arrange
        var path = "topic";

        // act
        var subject = new ConsumerBuilder<SomeMessage>(messageBusSettings)
            .Path(path);

        // assert
        subject.ConsumerSettings.Path.Should().Be(path);
        subject.ConsumerSettings.PathKind.Should().Be(PathKind.Topic);
    }

    [Fact]
    public void Given_Topic_Set_When_Configured_Then_Topic_ProperlySet()
    {
        // arrange
        var topic = "topic";

        // act
        var subject = new ConsumerBuilder<SomeMessage>(messageBusSettings)
            .Topic(topic);

        // assert
        subject.ConsumerSettings.Path.Should().Be(topic);
        subject.ConsumerSettings.PathKind.Should().Be(PathKind.Topic);
    }

    [Fact]
    public void Given_Instances_Set_When_Configured_Then_Instances_ProperlySet()
    {
        // arrange

        // act
        var subject = new ConsumerBuilder<SomeMessage>(messageBusSettings)
            .Instances(3);

        // assert
        subject.ConsumerSettings.Instances.Should().Be(3);
    }

    [Fact]
    public void Given_BaseMessageType_And_ItsHierarchy_When_WithConsumer_ForTheBaseTypeAndDerivedTypes_Then_TheConsumerSettingsAreCorrect()
    {
        // arrange
        var topic = "topic";

        var consumerContextMock = new Mock<IConsumerContext>();
        consumerContextMock.SetupGet(x => x.CancellationToken).Returns(new CancellationToken());

        // act
        var subject = new ConsumerBuilder<BaseMessage>(messageBusSettings)
            .Topic(topic)
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
    public void Given_BaseRequestType_And_ItsHierarchy_When_WithConsumer_ForTheBaseTypeAndDerivedTypes_Then_TheConsumerSettingsAreCorrect()
    {
        // arrange
        var topic = "topic";

        var consumerContextMock = new Mock<IConsumerContext>();
        consumerContextMock.SetupGet(x => x.CancellationToken).Returns(new CancellationToken());

        // act
        var subject = new ConsumerBuilder<BaseRequest>(messageBusSettings)
            .Topic(topic)
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
        public Task OnHandle(BaseMessage message) => throw new NotImplementedException(nameof(BaseMessage));
    }

    public class DerivedAMessageConsumer : IConsumer<DerivedAMessage>
    {
        public Task OnHandle(DerivedAMessage message) => throw new NotImplementedException(nameof(DerivedAMessage));
    }

    public class DerivedBMessageConsumer : IConsumer<DerivedBMessage>
    {
        public Task OnHandle(DerivedBMessage message) => throw new NotImplementedException(nameof(DerivedBMessage));
    }

    public class Derived2AMessageConsumer : IConsumer<Derived2AMessage>
    {
        public Task OnHandle(Derived2AMessage message) => throw new NotImplementedException(nameof(Derived2AMessage));
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
        public Task OnHandle(BaseRequest message) => throw new NotImplementedException(nameof(BaseRequest));
    }

    public class DerivedRequestConsumer : IConsumer<DerivedRequest>
    {
        public Task OnHandle(DerivedRequest message) => throw new NotImplementedException(nameof(DerivedRequest));
    }
}