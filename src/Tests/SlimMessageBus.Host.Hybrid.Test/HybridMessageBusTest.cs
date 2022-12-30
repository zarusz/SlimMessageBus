namespace SlimMessageBus.Host.Hybrid.Test;

using Newtonsoft.Json;
using System.Text;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.DependencyResolver;
using SlimMessageBus.Host.Serialization;
using System.Runtime;

public class HybridMessageBusTest
{
    private readonly Lazy<HybridMessageBus> _subject;
    private readonly MessageBusBuilder _messageBusBuilder;
    private readonly MessageBusSettings _settings = new();
    private readonly Mock<IDependencyResolver> _dependencyResolverMock = new();
    private readonly Mock<IMessageSerializer> _messageSerializerMock = new();

    private Mock<MessageBusBase> _bus1Mock;
    private Mock<MessageBusBase> _bus2Mock;

    public HybridMessageBusTest()
    {
        _messageBusBuilder = MessageBusBuilder.Create();

        _messageBusBuilder.Settings.DependencyResolver = _dependencyResolverMock.Object;
        _messageBusBuilder.Settings.Serializer = _messageSerializerMock.Object;

        _messageSerializerMock
            .Setup(x => x.Serialize(It.IsAny<Type>(), It.IsAny<object>()))
            .Returns((Type type, object message) => Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message)));
        _messageSerializerMock
            .Setup(x => x.Deserialize(It.IsAny<Type>(), It.IsAny<byte[]>()))
            .Returns((Type type, byte[] payload) => JsonConvert.DeserializeObject(Encoding.UTF8.GetString(payload), type));

        _messageBusBuilder.AddChildBus("bus1", (mbb) =>
        {
            mbb.Produce<SomeMessage>(x => x.DefaultTopic("topic1"));
            mbb.Produce<AnotherMessage>(x => x.DefaultTopic("topic2"));
            mbb.WithProvider(mbs =>
            {
                _bus1Mock = new Mock<MessageBusBase>(new[] { mbs });
                _bus1Mock.SetupGet(x => x.Settings).Returns(mbs);

                _bus1Mock.Setup(x => x.Publish(It.IsAny<SomeMessage>(), It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
                _bus1Mock.Setup(x => x.Publish(It.IsAny<AnotherMessage>(), It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

                return _bus1Mock.Object;
            });
        });
        _messageBusBuilder.AddChildBus("bus2", (mbb) =>
        {
            mbb.Produce<SomeMessage>(x => x.DefaultTopic("topic3"));
            mbb.Produce<SomeRequest>(x => x.DefaultTopic("topic4"));
            mbb.WithProvider(mbs =>
            {
                _bus2Mock = new Mock<MessageBusBase>(new[] { mbs });
                _bus2Mock.SetupGet(x => x.Settings).Returns(mbs);

                _bus2Mock.Setup(x => x.Publish(It.IsAny<SomeMessage>(), It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
                _bus2Mock.Setup(x => x.Send(It.IsAny<SomeRequest>(), It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), default, It.IsAny<TimeSpan?>())).Returns(Task.FromResult(new SomeResponse()));

                return _bus2Mock.Object;
            });
        });

        _subject = new Lazy<HybridMessageBus>(() => new HybridMessageBus(_messageBusBuilder.Settings, new HybridMessageBusSettings(), _messageBusBuilder));
    }

    [Fact]
    public async Task Given_DeclaredMessageTypeWithTwoBuses_When_Publish_Then_RoutesToBothBuses()
    {
        // arrange
        var someMessage = new SomeMessage();

        // act
        await _subject.Value.Publish(someMessage);

        // assert

        _bus1Mock.VerifyGet(x => x.Settings, Times.Once);
        _bus1Mock.Verify(x => x.Publish(someMessage, null, null, It.IsAny<CancellationToken>()));
        _bus1Mock.VerifyNoOtherCalls();

        _bus2Mock.VerifyGet(x => x.Settings, Times.Once);
        _bus2Mock.Verify(x => x.Publish(someMessage, null, null, It.IsAny<CancellationToken>()));
        _bus2Mock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Given_DeclaredMessageTypeWithOneBus_When_Publish_Then_RoutesToOnebus()
    {
        // arrange
        var anotherMessage = new AnotherMessage();

        // act
        await _subject.Value.Publish(anotherMessage);

        // assert

        _bus1Mock.VerifyGet(x => x.Settings, Times.Once);
        _bus1Mock.Verify(x => x.Publish(anotherMessage, null, null, It.IsAny<CancellationToken>()));
        _bus1Mock.VerifyNoOtherCalls();

        _bus2Mock.VerifyGet(x => x.Settings, Times.Once);
        _bus2Mock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Given_DeclaredMessageTypeAndItsAncestors_When_Publish_Then_RoutesToProperBus()
    {
        // arrange
        var someMessage = new SomeMessage();
        var someDerivedMessage = new SomeDerivedMessage();
        var someDerivedOfDerivedMessage = new SomeDerivedOfDerivedMessage();

        // act
        await _subject.Value.Publish(someMessage);
        await _subject.Value.Publish(someDerivedMessage);
        await _subject.Value.Publish<SomeMessage>(someDerivedMessage);
        await _subject.Value.Publish<ISomeMessageMarkerInterface>(someDerivedMessage);
        await _subject.Value.Publish(someDerivedOfDerivedMessage);
        await _subject.Value.Publish<SomeMessage>(someDerivedOfDerivedMessage);
        await _subject.Value.Publish<ISomeMessageMarkerInterface>(someDerivedOfDerivedMessage);

        // assert

        // note: Moq does not match exact generic types but with match with assignment compatibility
        // - cannot count the exact times a specific generic method ws executed
        // see https://stackoverflow.com/a/54721582
        _bus1Mock.Verify(x => x.Publish(someMessage, null, null, It.IsAny<CancellationToken>()));
        _bus1Mock.Verify(x => x.Publish(someDerivedMessage, null, null, It.IsAny<CancellationToken>()));
        _bus1Mock.Verify(x => x.Publish<SomeMessage>(someDerivedMessage, null, null, It.IsAny<CancellationToken>()));
        _bus1Mock.Verify(x => x.Publish<ISomeMessageMarkerInterface>(someDerivedMessage, null, null, It.IsAny<CancellationToken>()));
        _bus1Mock.Verify(x => x.Publish(someDerivedOfDerivedMessage, null, null, It.IsAny<CancellationToken>()));
        _bus1Mock.Verify(x => x.Publish<SomeMessage>(someDerivedOfDerivedMessage, null, null, It.IsAny<CancellationToken>()));
        _bus1Mock.Verify(x => x.Publish<ISomeMessageMarkerInterface>(someDerivedOfDerivedMessage, null, null, It.IsAny<CancellationToken>()));
        _bus1Mock.VerifyGet(x => x.Settings, Times.Once);
        _bus1Mock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Given_DeclaredRequestMessageTypeAndItsAncestors_When_Send_Then_RoutesToProperBus()
    {
        // arrange
        var someRequest = new SomeRequest();
        var someDerivedRequest = new SomeDerivedRequest();

        // act
        await _subject.Value.Send(someRequest);
        await _subject.Value.Send(someDerivedRequest);

        // assert
        _bus2Mock.Verify(x => x.Send(someRequest, null, null, default, null), Times.Once);
        _bus2Mock.Verify(x => x.Send(someDerivedRequest, null, null, default, null), Times.Once);
    }

    [Fact]
    public async Task Given_NotDeclaredMessageType_When_Publish_Then_ThrowsException()
    {
        // arrange

        // act
        Func<Task> notDeclaredTypePublish = () => _subject.Value.Publish("Fake Message");

        // assert
        await notDeclaredTypePublish.Should().ThrowAsync<ConfigurationMessageBusException>();
    }

    [Fact]
    public void Given_RequestMessageTypeDeclaredOnMoreThanOneBus_When_Constructor_Then_ThrowsException()
    {
        // arrange
        _messageBusBuilder.AddChildBus("bus3", (mbb) =>
        {
            mbb.Produce<SomeRequest>(x => x.DefaultTopic("topic5"));
            mbb.WithProvider(mbs =>
            {
                var bus3Mock = new Mock<MessageBusBase>(new[] { mbs });
                bus3Mock.SetupGet(x => x.Settings).Returns(mbs);
                return bus3Mock.Object;
            });
        });

        // act
        Action notDeclaredTypePublish = () => _ = _subject.Value;

        // assert
        notDeclaredTypePublish.Should().Throw<ConfigurationMessageBusException>();
    }


    internal interface ISomeMessageMarkerInterface
    {
    }

    internal class SomeMessage : ISomeMessageMarkerInterface
    {
    }

    internal class SomeDerivedMessage : SomeMessage
    {
    }

    internal class SomeDerivedOfDerivedMessage : SomeDerivedMessage
    {
    }

    internal class AnotherMessage
    {
    }

    internal class SomeRequest : IRequestMessage<SomeResponse>, ISomeMessageMarkerInterface
    {
    }

    internal class SomeDerivedRequest : SomeRequest
    {
    }

    internal class SomeResponse
    {
    }
}


