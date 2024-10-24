namespace SlimMessageBus.Host.Test.Hybrid;

using System.Text;

using Newtonsoft.Json;

using SlimMessageBus.Host;
using SlimMessageBus.Host.Hybrid;
using SlimMessageBus.Host.Test.Common;

public class HybridMessageBusTest
{
    private readonly Lazy<HybridMessageBus> _subject;
    private readonly MessageBusBuilder _messageBusBuilder;
    private readonly Mock<IServiceProvider> _serviceProviderMock = new();
    private readonly Mock<IMessageSerializer> _messageSerializerMock = new();
    private readonly Mock<ILoggerFactory> _loggerFactoryMock = new();
    private readonly Mock<ILogger<HybridMessageBusSettings>> _loggerMock = new();

    private Mock<MessageBusBase> _bus1Mock;
    private Mock<MessageBusBase> _bus2Mock;

    private readonly HybridMessageBusSettings _providerSettings;

    public HybridMessageBusTest()
    {
        _messageBusBuilder = MessageBusBuilder.Create();

        _messageBusBuilder.Settings.ServiceProvider = _serviceProviderMock.Object;

        _messageSerializerMock
            .Setup(x => x.Serialize(It.IsAny<Type>(), It.IsAny<object>()))
            .Returns((Type type, object message) => Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message)));
        _messageSerializerMock
            .Setup(x => x.Deserialize(It.IsAny<Type>(), It.IsAny<byte[]>()))
            .Returns((Type type, byte[] payload) => JsonConvert.DeserializeObject(Encoding.UTF8.GetString(payload), type));

        _serviceProviderMock.Setup(x => x.GetService(typeof(IMessageSerializer))).Returns(_messageSerializerMock.Object);
        _serviceProviderMock.Setup(x => x.GetService(typeof(IMessageTypeResolver))).Returns(new AssemblyQualifiedNameMessageTypeResolver());
        _serviceProviderMock.Setup(x => x.GetService(typeof(ILoggerFactory))).Returns(_loggerFactoryMock.Object);
        _serviceProviderMock.Setup(x => x.GetService(typeof(IEnumerable<IMessageBusLifecycleInterceptor>))).Returns(Array.Empty<IMessageBusLifecycleInterceptor>());
        _serviceProviderMock.Setup(x => x.GetService(typeof(ICurrentTimeProvider))).Returns(new CurrentTimeProvider());

        _loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_loggerMock.Object);

        _messageBusBuilder.AddChildBus("bus1", (mbb) =>
        {
            mbb.Produce<SomeMessage>(x => x.DefaultTopic("topic1"));
            mbb.Produce<AnotherMessage>(x => x.DefaultTopic("topic2"));
            mbb.WithProvider(mbs =>
            {
                _bus1Mock = new Mock<MessageBusBase>([mbs]);
                _bus1Mock.SetupGet(x => x.Settings).Returns(mbs);

                _bus1Mock.Setup(x => x.ProducePublish(It.IsAny<SomeMessage>(), It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<IMessageBusTarget>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
                _bus1Mock.Setup(x => x.ProducePublish(It.IsAny<AnotherMessage>(), It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<IMessageBusTarget>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

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

                _bus2Mock.Setup(x => x.ProducePublish(It.IsAny<SomeMessage>(), It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<IMessageBusTarget>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
                _bus2Mock.Setup(x => x.ProduceSend<SomeResponse>(It.IsAny<SomeRequest>(), It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<TimeSpan?>(), It.IsAny<IMessageBusTarget>(), default)).Returns(Task.FromResult(new SomeResponse()));

                return _bus2Mock.Object;
            });
        });

        _providerSettings = new();

        _subject = new Lazy<HybridMessageBus>(() => new HybridMessageBus(_messageBusBuilder.Settings, _providerSettings, _messageBusBuilder));
    }

    [Fact]
    public async Task When_Start_Then_StartChildBuses()
    {
        // act
        await _subject.Value.Start();

        // assert
        _bus1Mock.Verify(x => x.OnStart(), Times.Once);
        _bus2Mock.Verify(x => x.OnStart(), Times.Once);
    }

    [Fact]
    public async Task When_Stop_Then_StopChildBuses()
    {
        await _subject.Value.Start();

        // act
        await _subject.Value.Stop();

        // assert
        _bus1Mock.Verify(x => x.OnStop(), Times.Once);
        _bus2Mock.Verify(x => x.OnStop(), Times.Once);
    }

    [Fact]
    public async Task When_ProvisionTopology_Then_CallsProvisionTopologyOnChildBuses()
    {
        // act
        await _subject.Value.ProvisionTopology();

        // assert
        _bus1Mock.Verify(x => x.ProvisionTopology(), Times.Once);
        _bus2Mock.Verify(x => x.ProvisionTopology(), Times.Once);
    }

    [Theory]
    [InlineData("bus1", true)]
    [InlineData("bus2", true)]
    [InlineData("bus3", false)]
    public void When_GetChildBus_Then_ReturnsBusOrNull(string busName, bool returnsValue)
    {
        // act
        var childBus = _subject.Value.GetChildBus(busName);

        // assert
        if (returnsValue)
        {
            childBus.Should().NotBeNull();
        }
        else
        {
            childBus.Should().BeNull();
        }
    }

    [Fact]
    public void When_GetChildBuses_Then_ReturnsBuses()
    {
        // act
        var childBuses = _subject.Value.GetChildBuses();

        // assert
        childBuses.Should()
            .HaveCount(2)
            .And
            .Contain(x => ReferenceEquals(x, _bus1Mock.Object))
            .And
            .Contain(x => ReferenceEquals(x, _bus2Mock.Object));

    }

    [Theory]
    [InlineData(UndeclaredMessageTypeMode.DoNothing)]
    [InlineData(UndeclaredMessageTypeMode.RaiseOneTimeLog)]
    [InlineData(UndeclaredMessageTypeMode.RaiseException)]
    public async Task Given_UndeclareMessageType_When_Publish_Then_FollowsSettingsMode(UndeclaredMessageTypeMode mode)
    {
        // arrange
        _providerSettings.UndeclaredMessageTypeMode = mode;

        var message = new object();

        // act
        Func<Task> act = () => _subject.Value.ProducePublish(message);

        // assert
        if (mode == UndeclaredMessageTypeMode.RaiseException)
        {
            await act.Should().ThrowAsync<ConfigurationMessageBusException>();
        }
        else
        {
            await act.Should().NotThrowAsync();

            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((x, _) => MoqMatchers.LogMessageMatcher(x, m => m.StartsWith("Could not find any bus that produces the message type: "))),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), mode == UndeclaredMessageTypeMode.RaiseOneTimeLog ? Times.Once : Times.Never);
        }

        _bus1Mock.VerifyGet(x => x.Settings);
        _bus1Mock.VerifyNoOtherCalls();

        _bus2Mock.VerifyGet(x => x.Settings);
        _bus2Mock.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(UndeclaredMessageTypeMode.DoNothing)]
    [InlineData(UndeclaredMessageTypeMode.RaiseOneTimeLog)]
    [InlineData(UndeclaredMessageTypeMode.RaiseException)]
    public async Task Given_UndeclaredRequestType_When_Send_Then_FollowsSettingsMode(UndeclaredMessageTypeMode mode)
    {
        // arrange
        _providerSettings.UndeclaredMessageTypeMode = mode;

        var message = new SomeUndeclaredRequest();

        // act
        Func<Task<SomeResponse>> act = () => _subject.Value.ProduceSend<SomeResponse>(message);

        // assert
        if (mode == UndeclaredMessageTypeMode.RaiseException)
        {
            await act.Should().ThrowAsync<ConfigurationMessageBusException>();
        }
        else
        {
            var r = await act();
            r.Should().BeNull();

            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((x, _) => MoqMatchers.LogMessageMatcher(x, m => m.StartsWith("Could not find any bus that produces the message type: "))),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), mode == UndeclaredMessageTypeMode.RaiseOneTimeLog ? Times.Once : Times.Never);
        }

        _bus1Mock.VerifyGet(x => x.Settings);
        _bus1Mock.VerifyNoOtherCalls();

        _bus2Mock.VerifyGet(x => x.Settings);
        _bus2Mock.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(UndeclaredMessageTypeMode.DoNothing)]
    [InlineData(UndeclaredMessageTypeMode.RaiseOneTimeLog)]
    [InlineData(UndeclaredMessageTypeMode.RaiseException)]
    public async Task Given_UndeclaredRequestTypeWithoutResponse_When_Send_Then_FollowsSettingsMode(UndeclaredMessageTypeMode mode)
    {
        // arrange
        _providerSettings.UndeclaredMessageTypeMode = mode;

        var message = new SomeUndeclaredRequestWithoutResponse();

        // act
        Func<Task> act = () => _subject.Value.ProduceSend<object>(message);

        // assert
        if (mode == UndeclaredMessageTypeMode.RaiseException)
        {
            await act.Should().ThrowAsync<ConfigurationMessageBusException>();
        }
        else
        {
            await act.Should().NotThrowAsync();

            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((x, _) => MoqMatchers.LogMessageMatcher(x, m => m.StartsWith("Could not find any bus that produces the message type: "))),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), mode == UndeclaredMessageTypeMode.RaiseOneTimeLog ? Times.Once : Times.Never);
        }

        _bus1Mock.VerifyGet(x => x.Settings);
        _bus1Mock.VerifyNoOtherCalls();

        _bus2Mock.VerifyGet(x => x.Settings);
        _bus2Mock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Given_DeclaredMessageTypeWithTwoBuses_When_Publish_Then_RoutesToBothBuses()
    {
        // arrange
        var someMessage = new SomeMessage();

        // act
        await _subject.Value.ProducePublish(someMessage);

        // assert

        _bus1Mock.VerifyGet(x => x.Settings);
        _bus1Mock.Verify(x => x.ProducePublish(someMessage, null, null, null, It.IsAny<CancellationToken>()));
        _bus1Mock.VerifyNoOtherCalls();

        _bus2Mock.VerifyGet(x => x.Settings);
        _bus2Mock.Verify(x => x.ProducePublish(someMessage, null, null, null, It.IsAny<CancellationToken>()));
        _bus2Mock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Given_DeclaredMessageTypeWithOneBus_When_Publish_Then_RoutesToOnebus()
    {
        // arrange
        var anotherMessage = new AnotherMessage();

        // act
        await _subject.Value.ProducePublish(anotherMessage);

        // assert

        _bus1Mock.VerifyGet(x => x.Settings);
        _bus1Mock.Verify(x => x.ProducePublish(anotherMessage, null, null, null, It.IsAny<CancellationToken>()));
        _bus1Mock.VerifyNoOtherCalls();

        _bus2Mock.VerifyGet(x => x.Settings);
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
        await _subject.Value.ProducePublish(someMessage);
        await _subject.Value.ProducePublish(someDerivedMessage);
        await _subject.Value.ProducePublish(someDerivedOfDerivedMessage);

        // assert

        // note: Moq does not match exact generic types but with match with assignment compatibility
        // - cannot count the exact times a specific generic method ws executed
        // see https://stackoverflow.com/a/54721582
        _bus1Mock.Verify(x => x.ProducePublish(someMessage, null, null, null, It.IsAny<CancellationToken>()));
        _bus1Mock.Verify(x => x.ProducePublish(someDerivedMessage, null, null, null, It.IsAny<CancellationToken>()));
        _bus1Mock.Verify(x => x.ProducePublish(someDerivedOfDerivedMessage, null, null, null, It.IsAny<CancellationToken>()));
        _bus1Mock.VerifyGet(x => x.Settings);
        _bus1Mock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Given_DeclaredRequestMessageTypeAndItsAncestors_When_Send_Then_RoutesToProperBus()
    {
        // arrange
        var someRequest = new SomeRequest();
        var someDerivedRequest = new SomeDerivedRequest();

        // act
        await _subject.Value.ProduceSend<SomeResponse>(someRequest);
        await _subject.Value.ProduceSend<SomeResponse>(someDerivedRequest);

        // assert
        _bus2Mock.Verify(x => x.ProduceSend<SomeResponse>(someRequest, null, null, null, null, default), Times.Once);
        _bus2Mock.Verify(x => x.ProduceSend<SomeResponse>(someDerivedRequest, null, null, null, null, default), Times.Once);
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

    internal class SomeRequest : IRequest<SomeResponse>, ISomeMessageMarkerInterface
    {
    }

    internal class SomeDerivedRequest : SomeRequest
    {
    }

    internal class SomeResponse
    {
    }

    internal class SomeUndeclaredRequest : IRequest<SomeResponse>
    {
    }
    internal class SomeUndeclaredRequestWithoutResponse : IRequest
    {
    }
}


