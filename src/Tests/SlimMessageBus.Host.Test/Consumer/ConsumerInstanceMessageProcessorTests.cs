namespace SlimMessageBus.Host.Test;

using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.DependencyResolver;
using SlimMessageBus.Host.Interceptor;

public class ConsumerInstanceMessageProcessorTests
{
    private readonly MessageBusMock _busMock;
    private readonly Mock<Func<Type, byte[], object>> _messageProviderMock;

    public ConsumerInstanceMessageProcessorTests()
    {
        _busMock = new MessageBusMock();
        _messageProviderMock = new Mock<Func<Type, byte[], object>>();
    }

    [Fact]
    public async Task When_RequestExpired_Then_OnMessageExpiredIsCalled()
    {
        // arrange
        var onMessageExpiredMock = new Mock<Action<IMessageBus, AbstractConsumerSettings, object, object>>();

        var consumerSettings = new HandlerBuilder<SomeRequest, SomeResponse>(new MessageBusSettings()).Topic(null).WithHandler<IRequestHandler<SomeRequest, SomeResponse>>().ConsumerSettings;
        consumerSettings.OnMessageExpired = onMessageExpiredMock.Object;

        var transportMessage = Array.Empty<byte>();

        var request = new SomeRequest();
        var headers = new Dictionary<string, object>();
        headers.SetHeader(ReqRespMessageHeaders.Expires, _busMock.CurrentTime.AddSeconds(-10));
        headers.SetHeader(ReqRespMessageHeaders.RequestId, "request-id");

        object MessageProvider(Type messageType, byte[] payload) => request;

        var p = new ConsumerInstanceMessageProcessor<byte[]>(new[] { consumerSettings }, _busMock.Bus, MessageProvider, "path");

        _busMock.SerializerMock.Setup(x => x.Deserialize(typeof(SomeRequest), It.IsAny<byte[]>())).Returns(request);

        // act
        await p.ProcessMessage(transportMessage, headers);

        // assert
        _busMock.HandlerMock.Verify(x => x.OnHandle(It.IsAny<SomeRequest>(), It.IsAny<string>()), Times.Never); // the handler should not be called
        _busMock.HandlerMock.VerifyNoOtherCalls();

        onMessageExpiredMock.Verify(x => x(_busMock.Bus, consumerSettings, request, It.IsAny<object>()), Times.Once); // callback called once
        onMessageExpiredMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task When_RequestFails_Then_OnMessageFaultIsCalledAndErrorResponseIsSent()
    {
        // arrange
        var topic = "topic";
        var onMessageFaultMock = new Mock<Action<IMessageBus, AbstractConsumerSettings, object, Exception, object>>();
        var consumerSettings = new HandlerBuilder<SomeRequest, SomeResponse>(new MessageBusSettings()).Topic(topic).WithHandler<IRequestHandler<SomeRequest, SomeResponse>>().Instances(1).ConsumerSettings;
        consumerSettings.OnMessageFault = onMessageFaultMock.Object;

        var replyTo = "reply-topic";
        var requestId = "request-id";

        var transportMessage = Array.Empty<byte>();

        var request = new SomeRequest();
        var headers = new Dictionary<string, object>();
        headers.SetHeader(ReqRespMessageHeaders.RequestId, requestId);
        headers.SetHeader(ReqRespMessageHeaders.ReplyTo, replyTo);
        object MessageProvider(Type messageType, byte[] payload) => request;

        var p = new ConsumerInstanceMessageProcessor<byte[]>(new[] { consumerSettings }, _busMock.Bus, MessageProvider, topic);

        _busMock.SerializerMock.Setup(x => x.Deserialize(typeof(SomeRequest), It.IsAny<byte[]>())).Returns(request);

        var ex = new Exception("Something went bad");
        _busMock.HandlerMock.Setup(x => x.OnHandle(request, consumerSettings.Path)).Returns(Task.FromException<SomeResponse>(ex));

        // act
        var (exception, exceptionConsumerSettings, response) = await p.ProcessMessage(transportMessage, headers);

        // assert
        _busMock.HandlerMock.Verify(x => x.OnHandle(request, consumerSettings.Path), Times.Once); // handler called once
        _busMock.HandlerMock.VerifyNoOtherCalls();

        onMessageFaultMock.Verify(x => x(_busMock.Bus, consumerSettings, request, ex, It.IsAny<object>()), Times.Once); // callback called once
        onMessageFaultMock.VerifyNoOtherCalls();

        _busMock.BusMock.Verify(
            x => x.ProduceResponse(request, It.IsAny<IReadOnlyDictionary<string, object>>(), It.IsAny<SomeResponse>(), It.Is<IDictionary<string, object>>(m => (string)m[ReqRespMessageHeaders.RequestId] == requestId), It.IsAny<ConsumerSettings>()));

        exception.Should().BeNull();
        exceptionConsumerSettings.Should().BeNull();
    }

    [Fact]
    public async Task When_MessageFails_Then_OnMessageFaultIsCalledAndExceptionReturned()
    {
        // arrange
        var topic = "topic";
        var onMessageFaultMock = new Mock<Action<IMessageBus, AbstractConsumerSettings, object, Exception, object>>();
        var consumerSettings = new ConsumerBuilder<SomeMessage>(new MessageBusSettings()).Topic(topic).WithConsumer<IConsumer<SomeMessage>>().Instances(1).ConsumerSettings;
        consumerSettings.OnMessageFault = onMessageFaultMock.Object;

        var transportMessage = Array.Empty<byte>();

        var message = new SomeMessage();
        var messageHeaders = new Dictionary<string, object>();
        _messageProviderMock.Setup(x => x(message.GetType(), It.IsAny<byte[]>())).Returns(message);

        var ex = new Exception("Something went bad");
        _busMock.ConsumerMock.Setup(x => x.OnHandle(message, consumerSettings.Path)).ThrowsAsync(ex);

        var p = new ConsumerInstanceMessageProcessor<byte[]>(new[] { consumerSettings }, _busMock.Bus, _messageProviderMock.Object, topic);

        // act
        var (exception, exceptionConsumerSettings, response) = await p.ProcessMessage(transportMessage, messageHeaders);

        // assert
        _busMock.ConsumerMock.Verify(x => x.OnHandle(message, consumerSettings.Path), Times.Once); // handler called once
        _busMock.ConsumerMock.VerifyNoOtherCalls();

        onMessageFaultMock.Verify(x => x(_busMock.Bus, consumerSettings, message, ex, It.IsAny<object>()), Times.Once); // callback called once           
        onMessageFaultMock.VerifyNoOtherCalls();

        exception.Should().BeSameAs(exception);
        exceptionConsumerSettings.Should().BeSameAs(consumerSettings);
    }

    [Fact]
    public async Task When_MessageArrives_Then_OnMessageArrivedIsCalled()
    {
        // arrange
        var message = new SomeMessage();
        var topic = "topic1";

        var onMessageArrivedMock = new Mock<Action<IMessageBus, AbstractConsumerSettings, object, string, object>>();

        _busMock.Bus.Settings.OnMessageArrived = onMessageArrivedMock.Object;

        var consumerSettings = new ConsumerBuilder<SomeMessage>(_busMock.Bus.Settings).Topic(topic).WithConsumer<IConsumer<SomeMessage>>().ConsumerSettings;
        consumerSettings.OnMessageArrived = onMessageArrivedMock.Object;

        var transportMessage = Array.Empty<byte>();

        _messageProviderMock.Setup(x => x(message.GetType(), It.IsAny<byte[]>())).Returns(message);
        _busMock.ConsumerMock.Setup(x => x.OnHandle(message, consumerSettings.Path)).Returns(Task.CompletedTask);

        var p = new ConsumerInstanceMessageProcessor<byte[]>(new[] { consumerSettings }, _busMock.Bus, _messageProviderMock.Object, topic);

        // act
        await p.ProcessMessage(transportMessage, new Dictionary<string, object>());

        // assert
        _busMock.ConsumerMock.Verify(x => x.OnHandle(message, consumerSettings.Path), Times.Once); // handler called once
        _busMock.ConsumerMock.VerifyNoOtherCalls();

        onMessageArrivedMock.Verify(x => x(_busMock.Bus, consumerSettings, message, topic, It.IsAny<object>()), Times.Exactly(2)); // callback called once for consumer and bus level
        onMessageArrivedMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task When_MessageArrives_Then_ConsumerInterceptorIsCalled()
    {
        // arrange
        var message = new SomeMessage();
        var topic = "topic1";

        var messageConsumerInterceptor = new Mock<IConsumerInterceptor<SomeMessage>>();
        messageConsumerInterceptor
            .Setup(x => x.OnHandle(message, It.IsAny<CancellationToken>(), It.IsAny<Func<Task>>(), _busMock.Bus, topic, It.IsAny<IReadOnlyDictionary<string, object>>(), It.IsAny<object>()))
            .Callback((SomeMessage message, CancellationToken token, Func<Task> next, IMessageBus bus, string path, IReadOnlyDictionary<string, object> headers, object consumer) => next());

        _busMock.DependencyResolverMock.Setup(x => x.Resolve(typeof(IEnumerable<IConsumerInterceptor<SomeMessage>>))).Returns(new[] { messageConsumerInterceptor.Object });

        var consumerSettings = new ConsumerBuilder<SomeMessage>(_busMock.Bus.Settings).Topic(topic).WithConsumer<IConsumer<SomeMessage>>().ConsumerSettings;

        _messageProviderMock.Setup(x => x(message.GetType(), It.IsAny<byte[]>())).Returns(message);
        _busMock.ConsumerMock.Setup(x => x.OnHandle(message, consumerSettings.Path)).Returns(Task.CompletedTask);

        var p = new ConsumerInstanceMessageProcessor<byte[]>(new[] { consumerSettings }, _busMock.Bus, _messageProviderMock.Object, topic);

        // act
        await p.ProcessMessage(Array.Empty<byte>(), new Dictionary<string, object>());

        // assert
        _busMock.ConsumerMock.Verify(x => x.OnHandle(message, consumerSettings.Path), Times.Once); // handler called once
        _busMock.ConsumerMock.VerifyNoOtherCalls();

        messageConsumerInterceptor.Verify(x => x.OnHandle(message, It.IsAny<CancellationToken>(), It.IsAny<Func<Task>>(), _busMock.Bus, topic, It.IsAny<IReadOnlyDictionary<string, object>>(), _busMock.ConsumerMock.Object), Times.Once);
        messageConsumerInterceptor.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task When_MessageArrives_And_MessageScopeEnabled_Then_ScopeIsCreated_InstanceIsRetrivedFromScope_ConsumeMethodExecuted()
    {
        // arrange
        var topic = "topic1";

        var consumerSettings = new ConsumerBuilder<SomeMessage>(_busMock.Bus.Settings).Topic(topic).WithConsumer<IConsumer<SomeMessage>>().Instances(1).PerMessageScopeEnabled(true).ConsumerSettings;
        _busMock.BusMock.Setup(x => x.IsMessageScopeEnabled(consumerSettings)).Returns(true);

        var message = new SomeMessage();

        _messageProviderMock.Setup(x => x(message.GetType(), It.IsAny<byte[]>())).Returns(message);
        _busMock.ConsumerMock.Setup(x => x.OnHandle(message, topic)).Returns(Task.CompletedTask);

        var p = new ConsumerInstanceMessageProcessor<byte[]>(new[] { consumerSettings }, _busMock.Bus, _messageProviderMock.Object, topic);

        Mock<IChildDependencyResolver> childScopeMock = null;

        _busMock.OnChildDependencyResolverCreated = mock =>
        {
            childScopeMock = mock;
        };

        // act
        await p.ProcessMessage(Array.Empty<byte>(), new Dictionary<string, object>());

        // assert
        _busMock.ConsumerMock.Verify(x => x.OnHandle(message, consumerSettings.Path), Times.Once); // handler called once
        _busMock.DependencyResolverMock.Verify(x => x.CreateScope(), Times.Once);
        _busMock.ChildDependencyResolverMocks.Count.Should().Be(0); // it has been disposed
        childScopeMock.Should().NotBeNull();
        childScopeMock.Verify(x => x.Resolve(typeof(IConsumer<SomeMessage>)), Times.Once);
    }

    public static IEnumerable<object[]> Data => new List<object[]>
    {
        new object[] { new SomeMessage(), false, false },
        new object[] { new SomeDerivedMessage(), false, false },
        new object[] { new SomeRequest(), false, false },
        new object[] { new SomeDerived2Message(), false, false },
        new object[] { new object(), true, true, },
        new object[] { new object(), true, false },
    };

    [Theory]
    [MemberData(nameof(Data))]
    public async Task Given_SeveralConsumersOnSameTopic_When_MessageArrives_Then_MatchingConsumerExecuted(object message, bool isUndeclaredMessageType, bool shouldFailOnUndeclaredMessageType)
    {
        // arrange
        var topic = "topic";

        var consumerSettingsForSomeMessage = new ConsumerBuilder<SomeMessage>(_busMock.Bus.Settings)
            .Topic(topic)
            .WithConsumer<IConsumer<SomeMessage>>()
            .WithConsumer<IConsumer<SomeDerivedMessage>, SomeDerivedMessage>()
            .WhenUndeclaredMessageTypeArrives(opts =>
            {
                opts.Fail = shouldFailOnUndeclaredMessageType;
            })
            .ConsumerSettings;

        var consumerSettingsForSomeMessageInterface = new ConsumerBuilder<ISomeMessageMarkerInterface>(_busMock.Bus.Settings)
            .Topic(topic)
            .WithConsumer<IConsumer<ISomeMessageMarkerInterface>>()
            .ConsumerSettings;

        var consumerSettingsForSomeRequest = new HandlerBuilder<SomeRequest, SomeResponse>(_busMock.Bus.Settings)
            .Topic(topic)
            .WithHandler<IRequestHandler<SomeRequest, SomeResponse>>()
            .ConsumerSettings;

        var messageWithHeaderProviderMock = new Mock<Func<Type, byte[], object>>();

        var mesageHeaders = new Dictionary<string, object>
        {
            [MessageHeaders.MessageType] = _busMock.Bus.Settings.MessageTypeResolver.ToName(message.GetType())
        };

        var p = new ConsumerInstanceMessageProcessor<byte[]>(new[] { consumerSettingsForSomeMessage, consumerSettingsForSomeRequest, consumerSettingsForSomeMessageInterface }, _busMock.Bus, messageWithHeaderProviderMock.Object, topic);

        var transportMessage = new byte[] { 255 };

        _busMock.SerializerMock.Setup(x => x.Deserialize(message.GetType(), transportMessage)).Returns(message);
        messageWithHeaderProviderMock.Setup(x => x(message.GetType(), transportMessage)).Returns(message);

        var someMessageConsumerMock = new Mock<IConsumer<SomeMessage>>();
        var someMessageInterfaceConsumerMock = new Mock<IConsumer<ISomeMessageMarkerInterface>>();
        var someDerivedMessageConsumerMock = new Mock<IConsumer<SomeDerivedMessage>>();
        var someRequestMessageHandlerMock = new Mock<IRequestHandler<SomeRequest, SomeResponse>>();

        _busMock.DependencyResolverMock.Setup(x => x.Resolve(typeof(IConsumer<SomeMessage>))).Returns(someMessageConsumerMock.Object);
        _busMock.DependencyResolverMock.Setup(x => x.Resolve(typeof(IConsumer<ISomeMessageMarkerInterface>))).Returns(someMessageInterfaceConsumerMock.Object);
        _busMock.DependencyResolverMock.Setup(x => x.Resolve(typeof(IConsumer<SomeDerivedMessage>))).Returns(someDerivedMessageConsumerMock.Object);
        _busMock.DependencyResolverMock.Setup(x => x.Resolve(typeof(IRequestHandler<SomeRequest, SomeResponse>))).Returns(someRequestMessageHandlerMock.Object);

        someMessageConsumerMock.Setup(x => x.OnHandle(It.IsAny<SomeMessage>(), topic)).Returns(Task.CompletedTask);
        someMessageInterfaceConsumerMock.Setup(x => x.OnHandle(It.IsAny<ISomeMessageMarkerInterface>(), topic)).Returns(Task.CompletedTask);
        someDerivedMessageConsumerMock.Setup(x => x.OnHandle(It.IsAny<SomeDerivedMessage>(), topic)).Returns(Task.CompletedTask);
        someRequestMessageHandlerMock.Setup(x => x.OnHandle(It.IsAny<SomeRequest>(), topic)).Returns(Task.FromResult(new SomeResponse()));

        // act
        var (exception, consumerSettings, response) = await p.ProcessMessage(transportMessage, mesageHeaders);

        // assert
        consumerSettings.Should().BeNull();
        if (isUndeclaredMessageType)
        {
            if (shouldFailOnUndeclaredMessageType)
            {
                exception.Should().BeAssignableTo<MessageBusException>();
            }
            else
            {
                exception.Should().BeNull();
            }
        }
        else
        {
            exception.Should().BeNull();
        }

        if (message is SomeMessage someMessage)
        {
            someMessageConsumerMock.Verify(x => x.OnHandle(someMessage, topic), Times.Once);
        }
        someMessageConsumerMock.VerifyNoOtherCalls();

        if (message is ISomeMessageMarkerInterface someMessageInterface)
        {
            someMessageInterfaceConsumerMock.Verify(x => x.OnHandle(someMessageInterface, topic), Times.Once);
        }
        someMessageInterfaceConsumerMock.VerifyNoOtherCalls();

        if (message is SomeDerivedMessage someDerivedMessage)
        {
            someDerivedMessageConsumerMock.Verify(x => x.OnHandle(someDerivedMessage, topic), Times.Once);
        }
        someDerivedMessageConsumerMock.VerifyNoOtherCalls();

        if (message is SomeRequest someRequest)
        {
            someRequestMessageHandlerMock.Verify(x => x.OnHandle(someRequest, topic), Times.Once);
        }
        someRequestMessageHandlerMock.VerifyNoOtherCalls();
    }
}