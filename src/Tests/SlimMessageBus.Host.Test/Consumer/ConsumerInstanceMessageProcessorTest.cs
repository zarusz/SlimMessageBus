﻿namespace SlimMessageBus.Host.Test;

public class ConsumerInstanceMessageProcessorTest
{
    private readonly MessageBusMock _busMock;
    private readonly Mock<MessageProvider<byte[]>> _messageProviderMock;
    private readonly byte[] _transportMessage;
    private readonly string _topic;
    private readonly ConsumerSettings _handlerSettings;
    private readonly ConsumerSettings _consumerSettings;

    public ConsumerInstanceMessageProcessorTest()
    {
        _busMock = new MessageBusMock();
        _messageProviderMock = new Mock<MessageProvider<byte[]>>();
        _transportMessage = [];
        _topic = "topic";
        var messageBusSettings = new MessageBusSettings();
        _handlerSettings = new HandlerBuilder<SomeRequest, SomeResponse>(messageBusSettings).Topic(_topic).WithHandler<IRequestHandler<SomeRequest, SomeResponse>>().ConsumerSettings;
        _consumerSettings = new ConsumerBuilder<SomeMessage>(messageBusSettings).Topic(_topic).WithConsumer<IConsumer<SomeMessage>>().ConsumerSettings;
    }

    [Fact]
    public async Task When_ProcessMessage_ProcessesAsyncDisposableMessage_Then_MessageIsDisposed()
    {
        // arrange
        var mockMessage = new Mock<IAsyncDisposable>();
        mockMessage.Setup(x => x.DisposeAsync()).Verifiable();

        object MessageProvider(Type messageType, IReadOnlyDictionary<string, object> messageHeaders, object transportMessage) => mockMessage.Object;

        var p = new MessageProcessor<byte[]>([_handlerSettings], _busMock.Bus, MessageProvider, "path", responseProducer: _busMock.Bus);

        _busMock.SerializerMock.Setup(x => x.Deserialize(typeof(SomeRequest), It.IsAny<IReadOnlyDictionary<string, object>>(), It.IsAny<byte[]>(), It.IsAny<object>())).Returns(mockMessage.Object);

        // act
        await p.ProcessMessage(_transportMessage, new Dictionary<string, object>(), default);

        // assert
        mockMessage.Verify(x => x.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task When_ProcessMessage_ProcessesMessageThatExposesBothIDisposableAndIAsyncDisposable_Then_OnlyAsyncDisposableIsDisposed()
    {
        // arrange
        var mockMessage = new Mock<IDisposableAndIAsyncDisposable>();
        mockMessage.Setup(x => x.DisposeAsync()).Verifiable();
        mockMessage.Setup(x => x.Dispose()).Verifiable();

        object MessageProvider(Type messageType, IReadOnlyDictionary<string, object> messageHeaders, object transportMessage) => mockMessage.Object;

        var p = new MessageProcessor<byte[]>([_handlerSettings], _busMock.Bus, MessageProvider, "path", responseProducer: _busMock.Bus);

        _busMock.SerializerMock.Setup(x => x.Deserialize(typeof(SomeRequest), It.IsAny<IReadOnlyDictionary<string, object>>(), It.IsAny<byte[]>(), It.IsAny<object>())).Returns(mockMessage.Object);

        // act
        await p.ProcessMessage(_transportMessage, new Dictionary<string, object>(), default);

        // assert
        mockMessage.Verify(x => x.DisposeAsync(), Times.Once);
        mockMessage.Verify(x => x.Dispose(), Times.Never);
    }

    [Fact]
    public async Task When_ProcessMessage_ProcessesDisposableMessage_Then_MessageIsDisposed()
    {
        // arrange
        var mockMessage = new Mock<IDisposable>();
        mockMessage.Setup(x => x.Dispose()).Verifiable();

        object MessageProvider(Type messageType, IReadOnlyDictionary<string, object> messageHeaders, object transportMessage) => mockMessage.Object;

        var p = new MessageProcessor<byte[]>([_handlerSettings], _busMock.Bus, MessageProvider, "path", responseProducer: _busMock.Bus);

        _busMock.SerializerMock.Setup(x => x.Deserialize(typeof(SomeRequest), It.IsAny<IReadOnlyDictionary<string, object>>(), It.IsAny<byte[]>(), It.IsAny<object>())).Returns(mockMessage.Object);

        // act
        await p.ProcessMessage(_transportMessage, new Dictionary<string, object>(), default);

        // assert
        mockMessage.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task When_ProcessMessage_Given_ExpiredRequest_Then_HandlerNeverCalled_Nor_ProduceResponseCalled()
    {
        var requestId = "request-id";
        var request = new SomeRequest();
        var headers = new Dictionary<string, object>();
        headers.SetHeader(ReqRespMessageHeaders.Expires, _busMock.TimeProvider.GetUtcNow().Subtract(TimeSpan.FromSeconds(10)));
        headers.SetHeader(ReqRespMessageHeaders.RequestId, requestId);

        object MessageProvider(Type messageType, IReadOnlyDictionary<string, object> messageHeaders, object transportMessage) => request;

        var p = new MessageProcessor<byte[]>([_handlerSettings], _busMock.Bus, MessageProvider, "path", responseProducer: _busMock.Bus);

        _busMock.SerializerMock.Setup(x => x.Deserialize(typeof(SomeRequest), It.IsAny<IReadOnlyDictionary<string, object>>(), It.IsAny<byte[]>(), It.IsAny<object>())).Returns(request);

        // act
        await p.ProcessMessage(_transportMessage, headers, default);

        // assert
        _busMock.HandlerMock.Verify(x => x.OnHandle(It.IsAny<SomeRequest>(), It.IsAny<CancellationToken>()), Times.Never); // the handler should not be called
        _busMock.HandlerMock.VerifyNoOtherCalls();

        VerifyProduceResponseNeverCalled();
    }

    [Fact]
    public async Task When_ProcessMessage_Given_FailedRequest_Then_ErrorResponseIsSent_And_ProduceResponseIsCalled()
    {
        // arrange
        var replyTo = "reply-topic";
        var requestId = "request-id";

        var request = new SomeRequest();
        var headers = new Dictionary<string, object>();
        headers.SetHeader(ReqRespMessageHeaders.RequestId, requestId);
        headers.SetHeader(ReqRespMessageHeaders.ReplyTo, replyTo);
        object MessageProvider(Type messageType, IReadOnlyDictionary<string, object> messageHeaders, object transportMessage) => request;

        var p = new MessageProcessor<byte[]>([_handlerSettings], _busMock.Bus, MessageProvider, _topic, responseProducer: _busMock.Bus);

        _busMock.SerializerMock.Setup(x => x.Deserialize(typeof(SomeRequest), It.IsAny<IReadOnlyDictionary<string, object>>(), It.IsAny<byte[]>(), It.IsAny<object>())).Returns(request);

        var ex = new Exception("Something went bad");
        _busMock.HandlerMock.Setup(x => x.OnHandle(request, It.IsAny<CancellationToken>())).Returns(Task.FromException<SomeResponse>(ex));

        // act
        var result = await p.ProcessMessage(_transportMessage, headers, default);

        // assert
        result.Exception.Should().BeNull();
        result.Response.Should().BeNull();

        _busMock.HandlerMock.Verify(x => x.OnHandle(request, It.IsAny<CancellationToken>()), Times.Once); // handler called once
        _busMock.HandlerMock.VerifyNoOtherCalls();

        _busMock.BusMock.Verify(
            x => x.ProduceResponse(
                requestId,
                request,
                It.IsAny<IReadOnlyDictionary<string, object>>(),
                null,
                ex,
                It.IsAny<IMessageTypeConsumerInvokerSettings>(),
                It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task When_ProcessMessage_Given_FailedMessage_Then_ExceptionReturned()
    {
        // arrange
        var message = new SomeMessage();
        var messageHeaders = new Dictionary<string, object>();
        _messageProviderMock.Setup(x => x(message.GetType(), It.IsAny<IReadOnlyDictionary<string, object>>(), It.IsAny<byte[]>())).Returns(message);

        var ex = new Exception("Something went bad");
        _busMock.ConsumerMock.Setup(x => x.OnHandle(message, It.IsAny<CancellationToken>())).ThrowsAsync(ex);

        var p = new MessageProcessor<byte[]>([_consumerSettings], _busMock.Bus, _messageProviderMock.Object, _topic, responseProducer: _busMock.Bus);

        // act
        var result = await p.ProcessMessage(_transportMessage, messageHeaders, default);

        // assert
        result.Response.Should().BeNull();

        result.Exception.Should().BeSameAs(ex);
        result.ConsumerSettings.Should().BeSameAs(_consumerSettings);

        _busMock.ConsumerMock.Verify(x => x.OnHandle(message, It.IsAny<CancellationToken>()), Times.Once); // handler called once
        _busMock.ConsumerMock.VerifyNoOtherCalls();

        VerifyProduceResponseNeverCalled();
    }

    private void VerifyProduceResponseNeverCalled()
    {
        _busMock.BusMock.Verify(
            x => x.ProduceResponse(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<IReadOnlyDictionary<string, object>>(),
                It.IsAny<object>(),
                It.IsAny<Exception>(),
                It.IsAny<IMessageTypeConsumerInvokerSettings>(),
                It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task When_ProcessMessage_Given_ArrivedMessage_Then_MessageConsumerIsCalled()
    {
        // arrange
        var message = new SomeMessage();

        _messageProviderMock.Setup(x => x(message.GetType(), It.IsAny<IReadOnlyDictionary<string, object>>(), It.IsAny<byte[]>())).Returns(message);
        _busMock.ConsumerMock.Setup(x => x.OnHandle(message, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var p = new MessageProcessor<byte[]>([_consumerSettings], _busMock.Bus, _messageProviderMock.Object, _topic, responseProducer: _busMock.Bus);

        // act
        var result = await p.ProcessMessage(_transportMessage, new Dictionary<string, object>(), default);

        // assert
        result.Exception.Should().BeNull();
        result.Response.Should().BeNull();

        _busMock.ConsumerMock.Verify(x => x.OnHandle(message, It.IsAny<CancellationToken>()), Times.Once); // handler called once
        _busMock.ConsumerMock.VerifyNoOtherCalls();

        VerifyProduceResponseNeverCalled();
    }

    [Fact]
    public async Task When_ProcessMessage_Given_ArrivedMessage_Then_ConsumerInterceptorIsCalled()
    {
        // arrange
        var message = new SomeMessage();

        var messageConsumerInterceptor = new Mock<IConsumerInterceptor<SomeMessage>>();
        messageConsumerInterceptor
            .Setup(x => x.OnHandle(message, It.IsAny<Func<Task<object>>>(), It.IsAny<IConsumerContext>()))
            .Returns((SomeMessage message, Func<Task<object>> next, IConsumerContext context) => next());

        _busMock.ServiceProviderMock
            .Setup(x => x.GetService(typeof(IEnumerable<IConsumerInterceptor<SomeMessage>>)))
            .Returns(new[] { messageConsumerInterceptor.Object });

        _messageProviderMock.Setup(x => x(message.GetType(), It.IsAny<IReadOnlyDictionary<string, object>>(), It.IsAny<byte[]>())).Returns(message);
        _busMock.ConsumerMock.Setup(x => x.OnHandle(message, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var p = new MessageProcessor<byte[]>([_consumerSettings], _busMock.Bus, _messageProviderMock.Object, _topic, responseProducer: _busMock.Bus);

        // act
        var result = await p.ProcessMessage(Array.Empty<byte>(), new Dictionary<string, object>(), default);

        // assert
        result.Exception.Should().BeNull();
        result.Response.Should().BeNull();

        _busMock.ConsumerMock.Verify(x => x.OnHandle(message, It.IsAny<CancellationToken>()), Times.Once); // handler called once
        _busMock.ConsumerMock.VerifyNoOtherCalls();

        messageConsumerInterceptor.Verify(x => x.OnHandle(message, It.IsAny<Func<Task<object>>>(), It.IsAny<IConsumerContext>()), Times.Once);
        messageConsumerInterceptor.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task When_ProcessMessage_Given_RequestArrived_Then_RequestHandlerInterceptorIsCalled()
    {
        // arrange
        var request = new SomeRequest();
        var requestPayload = Array.Empty<byte>();
        var response = new SomeResponse();

        var handlerMock = new Mock<IRequestHandler<SomeRequest, SomeResponse>>();
        handlerMock
            .Setup(x => x.OnHandle(request, It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(response));

        var requestHandlerInterceptor = new Mock<IRequestHandlerInterceptor<SomeRequest, SomeResponse>>();
        requestHandlerInterceptor
            .Setup(x => x.OnHandle(request, It.IsAny<Func<Task<SomeResponse>>>(), It.IsAny<IConsumerContext>()))
            .Returns((SomeRequest message, Func<Task<SomeResponse>> next, IConsumerContext context) => next?.Invoke());

        _busMock.ServiceProviderMock
            .Setup(x => x.GetService(typeof(IRequestHandler<SomeRequest, SomeResponse>)))
            .Returns(handlerMock.Object);

        _busMock.ServiceProviderMock
            .Setup(x => x.GetService(typeof(IEnumerable<IRequestHandlerInterceptor<SomeRequest, SomeResponse>>)))
            .Returns(new[] { requestHandlerInterceptor.Object });

        _busMock.BusMock
            .Setup(x => x.ProduceResponse(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IReadOnlyDictionary<string, object>>(), It.IsAny<object>(), It.IsAny<Exception>(), It.IsAny<IMessageTypeConsumerInvokerSettings>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _messageProviderMock.Setup(x => x(request.GetType(), It.IsAny<IReadOnlyDictionary<string, object>>(), requestPayload)).Returns(request);

        var p = new MessageProcessor<byte[]>([_handlerSettings], _busMock.Bus, _messageProviderMock.Object, _topic, responseProducer: _busMock.Bus);

        // act
        var result = await p.ProcessMessage(requestPayload, new Dictionary<string, object>(), default);

        // assert
        result.Exception.Should().BeNull();
        result.Response.Should().BeSameAs(response);

        requestHandlerInterceptor.Verify(x => x.OnHandle(request, It.IsAny<Func<Task<SomeResponse>>>(), It.IsAny<IConsumerContext>()), Times.Once);
        requestHandlerInterceptor.VerifyNoOtherCalls();

        handlerMock.Verify(x => x.OnHandle(request, It.IsAny<CancellationToken>()), Times.Once); // handler called once
        handlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task When_ProcessMessage_Given_ArrivedRequestWithoutResponse_Then_RequestHandlerInterceptorIsCalled()
    {
        // arrange
        var request = new SomeRequestWithoutResponse();
        var requestPayload = Array.Empty<byte>();

        var handlerMock = new Mock<IRequestHandler<SomeRequestWithoutResponse>>();
        handlerMock
            .Setup(x => x.OnHandle(request, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var requestHandlerInterceptor = new Mock<IRequestHandlerInterceptor<SomeRequestWithoutResponse, Void>>();
        requestHandlerInterceptor
            .Setup(x => x.OnHandle(request, It.IsAny<Func<Task<Void>>>(), It.IsAny<IConsumerContext>()))
            .Returns((SomeRequestWithoutResponse message, Func<Task<Void>> next, IConsumerContext context) => next?.Invoke());

        _busMock.ServiceProviderMock
            .Setup(x => x.GetService(typeof(IRequestHandler<SomeRequestWithoutResponse>)))
            .Returns(handlerMock.Object);

        _busMock.ServiceProviderMock
            .Setup(x => x.GetService(typeof(IEnumerable<IRequestHandlerInterceptor<SomeRequestWithoutResponse, Void>>)))
            .Returns(new[] { requestHandlerInterceptor.Object });

        _busMock.BusMock
            .Setup(x => x.ProduceResponse(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IReadOnlyDictionary<string, object>>(), It.IsAny<object>(), It.IsAny<Exception>(), It.IsAny<IMessageTypeConsumerInvokerSettings>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var consumerSettings = new HandlerBuilder<SomeRequestWithoutResponse>(_busMock.Bus.Settings).Topic(_topic).WithHandler<IRequestHandler<SomeRequestWithoutResponse>>().ConsumerSettings;

        _messageProviderMock.Setup(x => x(request.GetType(), It.IsAny<IReadOnlyDictionary<string, object>>(), requestPayload)).Returns(request);

        var p = new MessageProcessor<byte[]>([consumerSettings], _busMock.Bus, _messageProviderMock.Object, _topic, responseProducer: _busMock.Bus);

        // act
        var result = await p.ProcessMessage(requestPayload, new Dictionary<string, object>(), default);

        // assert
        result.Exception.Should().BeNull();
        result.Response.Should().BeNull();

        requestHandlerInterceptor.Verify(x => x.OnHandle(request, It.IsAny<Func<Task<Void>>>(), It.IsAny<IConsumerContext>()), Times.Once);
        requestHandlerInterceptor.VerifyNoOtherCalls();

        handlerMock.Verify(x => x.OnHandle(request, It.IsAny<CancellationToken>()), Times.Once); // handler called once
        handlerMock.VerifyNoOtherCalls();
    }

    public class SomeMessageConsumerWithContext : IConsumer<SomeMessage>, IConsumerWithContext
    {
        public virtual IConsumerContext Context { get; set; }

        public virtual Task OnHandle(SomeMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Fact]
    public async Task When_ProcessMessage_Given_ArrivedMessage_And_ConsumerWithContext_Then_ConsumerContextIsSet()
    {
        // arrange
        var message = new SomeMessage();
        var headers = new Dictionary<string, object>();
        IConsumerContext context = null;
        CancellationToken cancellationToken = default;

        var consumerMock = new Mock<SomeMessageConsumerWithContext>();
        consumerMock.Setup(x => x.OnHandle(message, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        consumerMock.SetupSet(x => x.Context = It.IsAny<IConsumerContext>())
            .Callback<IConsumerContext>(p => context = p)
            .Verifiable();

        _busMock.ServiceProviderMock.Setup(x => x.GetService(typeof(SomeMessageConsumerWithContext))).Returns(consumerMock.Object);

        var consumerSettings = new ConsumerBuilder<SomeMessage>(_busMock.Bus.Settings).Topic(_topic).WithConsumer<SomeMessageConsumerWithContext>().ConsumerSettings;

        _messageProviderMock.Setup(x => x(message.GetType(), It.IsAny<IReadOnlyDictionary<string, object>>(), _transportMessage)).Returns(message);

        var p = new MessageProcessor<byte[]>([consumerSettings], _busMock.Bus, _messageProviderMock.Object, _topic, responseProducer: _busMock.Bus);

        // act
        await p.ProcessMessage(_transportMessage, headers, cancellationToken: cancellationToken);

        // assert
        consumerMock.Verify(x => x.OnHandle(message, It.IsAny<CancellationToken>()), Times.Once); // handler called once
        consumerMock.VerifySet(x => x.Context = It.IsAny<IConsumerContext>());
        consumerMock.VerifyNoOtherCalls();

        context.Should().NotBeNull();
        context.Path.Should().Be(_topic);
        context.CancellationToken.Should().Be(cancellationToken);
        context.Headers.Should().BeSameAs(headers);
        context.Consumer.Should().BeSameAs(consumerMock.Object);
    }

    [Fact]
    public async Task When_ProcessMessage_Given_ArrivedMessage_And_MessageScopeEnabled_Then_ScopeIsCreated_And_InstanceIsRetrivedFromScope_And_ConsumeMethodExecuted()
    {
        // arrange
        var consumerSettings = new ConsumerBuilder<SomeMessage>(_busMock.Bus.Settings).Topic(_topic).WithConsumer<IConsumer<SomeMessage>>().PerMessageScopeEnabled(true).ConsumerSettings;
        _busMock.BusMock.Setup(x => x.IsMessageScopeEnabled(consumerSettings, It.IsAny<IDictionary<string, object>>())).Returns(true);

        var message = new SomeMessage();

        _messageProviderMock.Setup(x => x(message.GetType(), It.IsAny<IReadOnlyDictionary<string, object>>(), It.IsAny<byte[]>())).Returns(message);
        _busMock.ConsumerMock.Setup(x => x.OnHandle(message, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var p = new MessageProcessor<byte[]>([consumerSettings], _busMock.Bus, _messageProviderMock.Object, _topic, responseProducer: _busMock.Bus);

        Mock<IServiceProvider> childScopeMock = null;

        _busMock.OnChildServiceProviderCreated = (_, mock) =>
        {
            childScopeMock = mock;
        };

        // act
        await p.ProcessMessage(_transportMessage, new Dictionary<string, object>(), default);

        // assert
        _busMock.ConsumerMock.Verify(x => x.OnHandle(message, It.IsAny<CancellationToken>()), Times.Once); // handler called once
        _busMock.ServiceProviderMock.Verify(x => x.GetService(typeof(IServiceScopeFactory)), Times.Once);
        _busMock.ChildServieProviderMocks.Count.Should().Be(0); // it has been disposed
        childScopeMock.Should().NotBeNull();
        childScopeMock.Verify(x => x.GetService(typeof(IConsumer<SomeMessage>)), Times.Once);
    }

    public static TheoryData<object, bool, bool> Data => new()
    {
        { new SomeMessage(), false, false },
        { new SomeDerivedMessage(), false, false },
        { new SomeRequest(), false, false },
        { new SomeDerived2Message(), false, false },
        { new object(), true, true },
        { new object(), true, false },
    };

    [Theory]
    [MemberData(nameof(Data))]
    public async Task When_ProcessMessage_Given_ArrivedMessage_And_SeveralConsumersOnSameTopic_Then_MatchingConsumerExecuted(object message, bool isUndeclaredMessageType, bool shouldFailOnUndeclaredMessageType)
    {
        // arrange
        var consumerSettingsForSomeMessage = new ConsumerBuilder<SomeMessage>(_busMock.Bus.Settings)
            .Topic(_topic)
            .WithConsumer<IConsumer<SomeMessage>>()
            .WithConsumer<IConsumer<SomeDerivedMessage>, SomeDerivedMessage>()
            .WhenUndeclaredMessageTypeArrives(opts =>
            {
                opts.Fail = shouldFailOnUndeclaredMessageType;
            })
            .ConsumerSettings;

        var consumerSettingsForSomeMessageInterface = new ConsumerBuilder<ISomeMessageMarkerInterface>(_busMock.Bus.Settings)
            .Topic(_topic)
            .WithConsumer<IConsumer<ISomeMessageMarkerInterface>>()
            .WhenUndeclaredMessageTypeArrives(opts =>
            {
                opts.Fail = shouldFailOnUndeclaredMessageType;
            })
            .ConsumerSettings;

        var consumerSettingsForSomeRequest = new HandlerBuilder<SomeRequest, SomeResponse>(_busMock.Bus.Settings)
            .Topic(_topic)
            .WithHandler<IRequestHandler<SomeRequest, SomeResponse>>()
            .WhenUndeclaredMessageTypeArrives(opts =>
            {
                opts.Fail = shouldFailOnUndeclaredMessageType;
            })
            .ConsumerSettings;

        var messageWithHeaderProviderMock = new Mock<MessageProvider<byte[]>>();

        var mesageHeaders = new Dictionary<string, object>
        {
            [MessageHeaders.MessageType] = _busMock.Bus.MessageTypeResolver.ToName(message.GetType())
        };

        var p = new MessageProcessor<byte[]>(
            [consumerSettingsForSomeMessage, consumerSettingsForSomeRequest, consumerSettingsForSomeMessageInterface],
            _busMock.Bus,
            messageWithHeaderProviderMock.Object,
            _topic,
            responseProducer: _busMock.Bus);

        _busMock.SerializerMock.Setup(x => x.Deserialize(message.GetType(), mesageHeaders, _transportMessage, It.IsAny<object>())).Returns(message);
        messageWithHeaderProviderMock.Setup(x => x(message.GetType(), It.IsAny<IReadOnlyDictionary<string, object>>(), _transportMessage)).Returns(message);

        var someMessageConsumerMock = new Mock<IConsumer<SomeMessage>>();
        var someMessageInterfaceConsumerMock = new Mock<IConsumer<ISomeMessageMarkerInterface>>();
        var someDerivedMessageConsumerMock = new Mock<IConsumer<SomeDerivedMessage>>();
        var someRequestMessageHandlerMock = new Mock<IRequestHandler<SomeRequest, SomeResponse>>();

        _busMock.ServiceProviderMock.Setup(x => x.GetService(typeof(IConsumer<SomeMessage>))).Returns(someMessageConsumerMock.Object);
        _busMock.ServiceProviderMock.Setup(x => x.GetService(typeof(IConsumer<ISomeMessageMarkerInterface>))).Returns(someMessageInterfaceConsumerMock.Object);
        _busMock.ServiceProviderMock.Setup(x => x.GetService(typeof(IConsumer<SomeDerivedMessage>))).Returns(someDerivedMessageConsumerMock.Object);
        _busMock.ServiceProviderMock.Setup(x => x.GetService(typeof(IRequestHandler<SomeRequest, SomeResponse>))).Returns(someRequestMessageHandlerMock.Object);

        someMessageConsumerMock.Setup(x => x.OnHandle(It.IsAny<SomeMessage>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        someMessageInterfaceConsumerMock.Setup(x => x.OnHandle(It.IsAny<ISomeMessageMarkerInterface>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        someDerivedMessageConsumerMock.Setup(x => x.OnHandle(It.IsAny<SomeDerivedMessage>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        someRequestMessageHandlerMock.Setup(x => x.OnHandle(It.IsAny<SomeRequest>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(new SomeResponse()));

        // act
        var result = await p.ProcessMessage(_transportMessage, mesageHeaders, default);

        // assert
        result.ConsumerSettings.Should().BeNull();
        if (isUndeclaredMessageType)
        {
            if (shouldFailOnUndeclaredMessageType)
            {
                result.Exception.Should().BeAssignableTo<ConsumerMessageBusException>();
                result.Result.Should().Be(ProcessResult.Failure);
            }
            else
            {
                result.Exception.Should().BeNull();
                result.Result.Should().Be(ProcessResult.Success);
            }
        }
        else
        {
            result.Exception.Should().BeNull();
            result.Result.Should().Be(ProcessResult.Success);
        }

        if (message is SomeMessage someMessage)
        {
            someMessageConsumerMock.Verify(x => x.OnHandle(someMessage, It.IsAny<CancellationToken>()), Times.Once);
        }
        someMessageConsumerMock.VerifyNoOtherCalls();

        if (message is ISomeMessageMarkerInterface someMessageInterface)
        {
            someMessageInterfaceConsumerMock.Verify(x => x.OnHandle(someMessageInterface, It.IsAny<CancellationToken>()), Times.Once);
        }
        someMessageInterfaceConsumerMock.VerifyNoOtherCalls();

        if (message is SomeDerivedMessage someDerivedMessage)
        {
            someDerivedMessageConsumerMock.Verify(x => x.OnHandle(someDerivedMessage, It.IsAny<CancellationToken>()), Times.Once);
        }
        someDerivedMessageConsumerMock.VerifyNoOtherCalls();

        if (message is SomeRequest someRequest)
        {
            someRequestMessageHandlerMock.Verify(x => x.OnHandle(someRequest, It.IsAny<CancellationToken>()), Times.Once);
        }
        someRequestMessageHandlerMock.VerifyNoOtherCalls();
    }

    public interface IDisposableAndIAsyncDisposable : IAsyncDisposable, IDisposable
    {
    }
}
