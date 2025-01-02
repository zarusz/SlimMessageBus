namespace SlimMessageBus.Host.Test;

using SlimMessageBus.Host.Consumer;

public class MessageHandlerTest
{
    private readonly MessageBusMock busMock;
    private readonly Mock<IMessageScope> messageScopeMock;
    private readonly Mock<IMessageScopeFactory> messageScopeFactoryMock;
    private readonly Mock<IMessageTypeResolver> messageTypeResolverMock;
    private readonly Mock<IMessageHeadersFactory> messageHeaderFactoryMock;
    private readonly Mock<IConsumerContext> consumerContextMock;
    private readonly Mock<IMessageTypeConsumerInvokerSettings> consumerInvokerMock;
    private readonly Mock<ConsumerMethod> consumerMethodMock;
    private readonly MessageHandler subject;
    private readonly Fixture fixture = new();

    public MessageHandlerTest()
    {
        busMock = new();

        messageScopeMock = new Mock<IMessageScope>();
        messageScopeFactoryMock = new Mock<IMessageScopeFactory>();
        messageScopeFactoryMock
            .Setup(x => x.CreateMessageScope(It.IsAny<ConsumerSettings>(), It.IsAny<object>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<IServiceProvider>()))
            .Returns(messageScopeMock.Object);

        messageTypeResolverMock = new Mock<IMessageTypeResolver>();
        messageHeaderFactoryMock = new Mock<IMessageHeadersFactory>();
        consumerContextMock = new Mock<IConsumerContext>();
        consumerInvokerMock = new Mock<IMessageTypeConsumerInvokerSettings>();

        consumerMethodMock = new Mock<ConsumerMethod>();
        consumerInvokerMock.SetupGet(x => x.ConsumerMethod).Returns(consumerMethodMock.Object);

        subject = new MessageHandler(
            messageBus: busMock.Bus,
            messageScopeFactory: messageScopeFactoryMock.Object,
            messageTypeResolver: messageTypeResolverMock.Object,
            messageHeadersFactory: messageHeaderFactoryMock.Object,
            runtimeTypeCache: new Host.Collections.RuntimeTypeCache(),
            currentTimeProvider: busMock,
            path: "topic1");
    }

    [Fact]
    public async Task When_ExecuteConsumer_Given_Consumer_Then_ReturnsResponse()
    {
        // arrange
        var consumerMock = new Mock<SomeMessageConsumer>();

        var someMessage = new SomeMessage();

        consumerContextMock.SetupGet(x => x.Consumer).Returns(consumerMock.Object);
        consumerContextMock.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        consumerMethodMock.Setup(x => x(consumerMock.Object, someMessage, consumerContextMock.Object, consumerContextMock.Object.CancellationToken)).Returns(Task.CompletedTask);

        // act
        var result = await subject.ExecuteConsumer(someMessage, consumerContextMock.Object, consumerInvokerMock.Object, null);

        // assert
        result.Should().BeNull();

        consumerMethodMock.Verify(x => x(consumerMock.Object, someMessage, consumerContextMock.Object, consumerContextMock.Object.CancellationToken), Times.Once());
        consumerMethodMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task When_ExecuteConsumer_Given_ConsumerWithContext_Then_ConsumerContextSet()
    {
        // arrange
        var consumerMock = new Mock<IConsumerWithContext>();

        var someMessage = new SomeMessage();

        consumerContextMock.SetupGet(x => x.Consumer).Returns(consumerMock.Object);
        consumerContextMock.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        consumerMethodMock.Setup(x => x(consumerMock.Object, someMessage, consumerContextMock.Object, consumerContextMock.Object.CancellationToken)).Returns(Task.CompletedTask);

        // act
        var result = await subject.ExecuteConsumer(someMessage, consumerContextMock.Object, consumerInvokerMock.Object, null);

        // assert
        consumerMock.VerifySet(x => { x.Context = consumerContextMock.Object; }, Times.Once());
        consumerMock.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task When_DoHandle_Given_ConsumerThatThrowsExceptionAndErrorHandlerRegisteredAndIsAbleToHandleException_Then_ErrorHandlerIsInvoked(bool errorHandlerRegistered, bool errorHandlerWasAbleToHandle)
    {
        // arrange
        var someMessage = new SomeMessage();
        var someException = fixture.Create<Exception>();
        var messageHeaders = fixture.Create<Dictionary<string, object>>();

        var consumerMock = new Mock<IConsumer<SomeMessage>>();

        var consumerErrorHandlerMock = new Mock<IConsumerErrorHandler<SomeMessage>>();

        consumerErrorHandlerMock
            .Setup(x => x.OnHandleError(someMessage, It.IsAny<IConsumerContext>(), someException, It.IsAny<int>()))
            .ReturnsAsync(() => errorHandlerWasAbleToHandle ? ProcessResult.Success : ProcessResult.Failure);

        if (errorHandlerRegistered)
        {
            busMock.ServiceProviderMock
                .Setup(x => x.GetService(typeof(IConsumerErrorHandler<SomeMessage>)))
                .Returns(consumerErrorHandlerMock.Object);
        }

        busMock.ServiceProviderMock
            .Setup(x => x.GetService(typeof(IConsumer<SomeMessage>)))
            .Returns(consumerMock.Object);

        consumerMethodMock
            .Setup(x => x(consumerMock.Object, someMessage, It.IsAny<IConsumerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(someException);

        consumerInvokerMock
            .SetupGet(x => x.ParentSettings)
            .Returns(new ConsumerSettings());
        consumerInvokerMock
            .SetupGet(x => x.ConsumerType)
            .Returns(typeof(IConsumer<SomeMessage>));

        messageScopeMock
            .SetupGet(x => x.ServiceProvider)
            .Returns(busMock.ServiceProviderMock.Object);

        // act
        var result = await subject.DoHandle(someMessage, messageHeaders, consumerInvoker: consumerInvokerMock.Object, currentServiceProvider: busMock.ServiceProviderMock.Object);

        // assert
        result.RequestId.Should().BeNull();
        result.Response.Should().BeNull();

        if (errorHandlerRegistered && errorHandlerWasAbleToHandle)
        {
            result.ResponseException.Should().BeNull();
        }
        else
        {
            result.ResponseException.Should().BeSameAs(someException);
        }

        consumerMethodMock
            .Verify(x => x(consumerMock.Object, someMessage, It.IsAny<IConsumerContext>(), It.IsAny<CancellationToken>()), Times.Once());
        consumerMethodMock
            .VerifyNoOtherCalls();

        if (errorHandlerRegistered)
        {
            consumerErrorHandlerMock
                .Verify(
                    x => x.OnHandleError(someMessage, It.IsAny<IConsumerContext>(), someException, It.IsAny<int>()),
                    Times.Once());

            consumerErrorHandlerMock
                .VerifyNoOtherCalls();
        }
    }

    [Fact]
    public async Task When_DoHandle_Given_ConsumerThatThrowsExceptionAndErrorHandlerRegisteredAndRequestsARetry_Then_RetryInvocation()
    {
        // arrange
        var someMessage = new SomeMessage();
        var someException = fixture.Create<Exception>();
        var messageHeaders = fixture.Create<Dictionary<string, object>>();

        var consumerMock = new Mock<IConsumer<SomeMessage>>();

        var consumerErrorHandlerMock = new Mock<IConsumerErrorHandler<SomeMessage>>();

        consumerErrorHandlerMock
            .Setup(x => x.OnHandleError(someMessage, It.IsAny<IConsumerContext>(), someException, It.IsAny<int>()))
            .ReturnsAsync(() => ProcessResult.Retry);

        busMock.ServiceProviderMock
            .Setup(x => x.GetService(typeof(IConsumerErrorHandler<SomeMessage>)))
            .Returns(consumerErrorHandlerMock.Object);

        busMock.ServiceProviderMock
            .Setup(x => x.GetService(typeof(IConsumer<SomeMessage>)))
            .Returns(consumerMock.Object);

        consumerMethodMock
            .SetupSequence(x => x(consumerMock.Object, someMessage, It.IsAny<IConsumerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(someException)
            .Returns(Task.CompletedTask);

        consumerInvokerMock
            .SetupGet(x => x.ParentSettings)
            .Returns(new ConsumerSettings());

        consumerInvokerMock
            .SetupGet(x => x.ConsumerType)
            .Returns(typeof(IConsumer<SomeMessage>));

        messageScopeMock
            .SetupGet(x => x.ServiceProvider)
            .Returns(busMock.ServiceProviderMock.Object);

        // act
        var result = await subject.DoHandle(someMessage, messageHeaders, consumerInvoker: consumerInvokerMock.Object, currentServiceProvider: busMock.ServiceProviderMock.Object);

        // assert
        result.RequestId.Should().BeNull();
        result.Response.Should().BeNull();
        result.ResponseException.Should().BeNull();

        messageScopeFactoryMock
            .Verify(x => x.CreateMessageScope(It.IsAny<ConsumerSettings>(), It.IsAny<object>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<IServiceProvider>()), Times.Exactly(2));

        consumerMethodMock
            .Verify(x => x(consumerMock.Object, someMessage, It.IsAny<IConsumerContext>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

        consumerMethodMock
            .VerifyNoOtherCalls();

        consumerErrorHandlerMock
            .Verify(
                x => x.OnHandleError(someMessage, It.IsAny<IConsumerContext>(), someException, It.IsAny<int>()),
                Times.Once());

        consumerErrorHandlerMock
            .VerifyNoOtherCalls();
    }

    [Fact]
    public async Task When_ExecuteConsumer_Given_Handler_Then_ReturnsResponse()
    {
        // arrange
        var handlerMock = new Mock<SomeRequestMessageHandler>();

        var someRequest = new SomeRequest();
        var someResponse = new SomeResponse();

        consumerContextMock.SetupGet(x => x.Consumer).Returns(handlerMock.Object);
        consumerContextMock.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        consumerMethodMock.Setup(x => x(handlerMock.Object, someRequest, consumerContextMock.Object, consumerContextMock.Object.CancellationToken)).Returns(Task.FromResult(someResponse));

        // act
        var result = await subject.ExecuteConsumer(someRequest, consumerContextMock.Object, consumerInvokerMock.Object, typeof(SomeResponse));

        // assert
        result.Should().BeSameAs(someResponse);

        consumerMethodMock.Verify(x => x(handlerMock.Object, someRequest, consumerContextMock.Object, consumerContextMock.Object.CancellationToken), Times.Once());
        consumerMethodMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task When_ExecuteConsumer_Given_HandlerWithoutResponse_Then_ReturnsNull()
    {
        // arrange
        var handlerMock = new Mock<SomeRequestWithoutResponseHandler>();

        var someRequestWithoutResponse = new SomeRequestWithoutResponse();

        consumerContextMock.SetupGet(x => x.Consumer).Returns(handlerMock.Object);
        consumerContextMock.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        consumerMethodMock.Setup(x => x(handlerMock.Object, someRequestWithoutResponse, consumerContextMock.Object, consumerContextMock.Object.CancellationToken)).Returns(Task.CompletedTask);

        // act
        var result = await subject.ExecuteConsumer(someRequestWithoutResponse, consumerContextMock.Object, consumerInvokerMock.Object, typeof(Void));

        // assert
        result.Should().BeNull();

        consumerMethodMock.Verify(x => x(handlerMock.Object, someRequestWithoutResponse, consumerContextMock.Object, consumerContextMock.Object.CancellationToken), Times.Once());
        consumerMethodMock.VerifyNoOtherCalls();
    }
}