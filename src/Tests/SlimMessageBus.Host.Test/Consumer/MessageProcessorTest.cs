namespace SlimMessageBus.Host.Test;

using SlimMessageBus.Host.Consumer;
using SlimMessageBus.Host.Test.Messages;

public class MessageProcessorTest
{
    private readonly MessageBusMock busMock;
    private readonly Mock<IMessageScope> messageScopeMock;
    private readonly Mock<IMessageScopeFactory> messageScopeFactoryMock;
    private readonly Mock<IMessageTypeResolver> messageTypeResolverMock;
    private readonly Mock<IMessageTypeConsumerInvokerSettings> consumerInvokerMock;
    private readonly Mock<ConsumerMethod> consumerMethodMock;
    private readonly Mock<IResponseProducer> responseProducerMock;
    private readonly Mock<MessageProvider<SomeMessage>> messageProviderMock;
    private readonly ConsumerBuilder<SomeMessage> consumerBuilder;
    private readonly Lazy<MessageProcessor<SomeMessage>> subject;

    public MessageProcessorTest()
    {
        busMock = new();

        messageScopeMock = new Mock<IMessageScope>();
        messageScopeFactoryMock = new Mock<IMessageScopeFactory>();
        messageScopeFactoryMock
            .Setup(x => x.CreateMessageScope(It.IsAny<ConsumerSettings>(), It.IsAny<object>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<IServiceProvider>()))
            .Returns(messageScopeMock.Object);

        messageTypeResolverMock = new Mock<IMessageTypeResolver>();
        consumerInvokerMock = new Mock<IMessageTypeConsumerInvokerSettings>();

        consumerMethodMock = new Mock<ConsumerMethod>();
        consumerInvokerMock.SetupGet(x => x.ConsumerMethod).Returns(consumerMethodMock.Object);

        responseProducerMock = new Mock<IResponseProducer>();
        messageProviderMock = new Mock<MessageProvider<SomeMessage>>();

        consumerBuilder = new ConsumerBuilder<SomeMessage>(new MessageBusSettings());
        consumerBuilder.WithConsumer<SomeMessageConsumer>();

        subject = new Lazy<MessageProcessor<SomeMessage>>(
            () => new MessageProcessor<SomeMessage>(
                consumerSettings: [consumerBuilder.ConsumerSettings],
                messageBus: busMock.Bus,
                messageProvider: messageProviderMock.Object,
                path: "topic1",
                responseProducer: responseProducerMock.Object)
            );
    }

    [Fact]
    public async Task CancellationDuringRoutingReturnsCanceledWithoutInvokingTheConsumer()
    {
        using var cancellation = new CancellationTokenSource();
        consumerBuilder.Filter((_, _) =>
        {
            cancellation.Cancel();
            return true;
        });
        var message = new SomeMessage();

        var result = await subject.Value.ProcessMessage(message, new Dictionary<string, object>(), cancellationToken: cancellation.Token);

        result.Result.Should().Be(ProcessResult.Canceled);
        result.Exception.Should().BeOfType<OperationCanceledException>().Which.CancellationToken.Should().Be(cancellation.Token);
        messageProviderMock.VerifyNoOtherCalls();
        responseProducerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CanceledRequestDoesNotSendAnErrorReply()
    {
        using var cancellation = new CancellationTokenSource();
        var request = new SomeRequest();
        var transportMessage = new SomeMessage();
        var headers = new Dictionary<string, object>();
        var handlerSettings = new HandlerBuilder<SomeRequest, SomeResponse>(new MessageBusSettings())
            .WithHandler<IRequestHandler<SomeRequest, SomeResponse>>().ConsumerSettings;
        handlerSettings.IsMessageScopeEnabled = false;
        var processor = new MessageProcessor<SomeMessage>([handlerSettings], busMock.Bus, messageProviderMock.Object, "topic1", responseProducerMock.Object);
        messageProviderMock.Setup(x => x(typeof(SomeRequest), headers, transportMessage)).Returns(request);
        busMock.HandlerMock.Setup(x => x.OnHandle(request, cancellation.Token)).Returns(() =>
        {
            cancellation.Cancel();
            return Task.FromCanceled<SomeResponse>(cancellation.Token);
        });

        var result = await processor.ProcessMessage(transportMessage, headers, cancellationToken: cancellation.Token);

        result.Result.Should().Be(ProcessResult.Canceled);
        result.Exception.Should().BeAssignableTo<OperationCanceledException>();
        responseProducerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task When_ProcessMessage_Given_MessagePayloadCannotDeserialize_Then_ResultIsFailure()
    {
        // arrange
        var transportMessageMock = new Mock<SomeMessage>();
        var headers = new Dictionary<string, object>
        {
            { MessageHeaders.MessageType, typeof(SomeMessage).AssemblyQualifiedName }
        };

        var deserializationException = new InvalidOperationException("Deserialization failed");

        messageProviderMock
            .Setup(x => x.Invoke(typeof(SomeMessage), headers, transportMessageMock.Object))
            .Throws(deserializationException);

        // act
        var result = await subject.Value.ProcessMessage(transportMessageMock.Object, headers);

        // assert
        messageProviderMock.Verify(x => x(typeof(SomeMessage), headers, transportMessageMock.Object), Times.Once);
        messageProviderMock.VerifyNoOtherCalls();

        result.Should().NotBeNull();
        result.Result.Should().Be(ProcessResult.Failure);
        result.Exception.Should().NotBeNull();
    }

    [Fact]
    public async Task When_ProcessMessage_Given_RequestPayloadCannotDeserialize_Then_ErrorResponseIsSent()
    {
        // arrange
        var transportMessageMock = new Mock<SomeMessage>();
        var headers = new Dictionary<string, object>
        {
            { MessageHeaders.MessageType, typeof(SomeRequest).AssemblyQualifiedName },
            { ReqRespMessageHeaders.RequestId, "request-id" },
            { ReqRespMessageHeaders.ReplyTo, "reply-to" }
        };

        var deserializationException = new InvalidOperationException("Deserialization failed");
        var handlerBuilder = new HandlerBuilder<SomeRequest, SomeResponse>(new MessageBusSettings());
        handlerBuilder.WithHandler<SomeRequestMessageHandler>();

        var subject = new MessageProcessor<SomeMessage>(
            consumerSettings: [handlerBuilder.ConsumerSettings],
            messageBus: busMock.Bus,
            messageProvider: messageProviderMock.Object,
            path: "topic1",
            responseProducer: responseProducerMock.Object);

        messageProviderMock
            .Setup(x => x.Invoke(typeof(SomeRequest), headers, transportMessageMock.Object))
            .Throws(deserializationException);

        // act
        var result = await subject.ProcessMessage(transportMessageMock.Object, headers);

        // assert
        messageProviderMock.Verify(x => x(typeof(SomeRequest), headers, transportMessageMock.Object), Times.Once);
        responseProducerMock.Verify(
            x => x.ProduceResponse(
                "request-id",
                null,
                headers,
                null,
                deserializationException,
                handlerBuilder.ConsumerSettings,
                It.IsAny<CancellationToken>()),
            Times.Once);

        result.Should().NotBeNull();
        result.Result.Should().Be(ProcessResult.Failure);
        result.Exception.Should().BeNull();
    }

    [Theory]
    [InlineData(null, "UnknownMessage")]
    [InlineData(true, "UnknownMessage")]
    [InlineData(false, "UnknownMessage")]
    [InlineData(null, nameof(SomeMessage2))]
    [InlineData(true, nameof(SomeMessage2))]
    [InlineData(false, nameof(SomeMessage2))]
    public async Task When_ProcessMessage_Given_MessageTypeHeaderValueUnknown_Then_ResultIsFailureOrSuccess(bool? shouldFailWhenUnrecognizedMessageType, string messageTypeName)
    {
        // arrange
        var transportMessageMock = new Mock<SomeMessage>();
        var headers = new Dictionary<string, object>
        {
            // corrupt the header value
            { MessageHeaders.MessageType, typeof(SomeMessage).AssemblyQualifiedName.Replace(nameof(SomeMessage), messageTypeName) }
        };

        if (shouldFailWhenUnrecognizedMessageType is not null)
        {
            consumerBuilder.WhenUndeclaredMessageTypeArrives(s =>
            {
                s.Fail = shouldFailWhenUnrecognizedMessageType.Value;
            });
        }

        // act
        var result = await subject.Value.ProcessMessage(transportMessageMock.Object, headers);

        // assert
        result.Should().NotBeNull();
        if (shouldFailWhenUnrecognizedMessageType ?? true) // by default when not configured explicitly we expect uknown message type to succeed
        {
            result.Result.Should().Be(ProcessResult.Failure);
            result.Exception.Should().NotBeNull();
            result.Exception.Message.Should().Contain("is not a known type");
        }
        else
        {
            result.Result.Should().Be(ProcessResult.Success);
            result.Exception.Should().BeNull();
        }

        messageProviderMock.VerifyNoOtherCalls();
    }
}
