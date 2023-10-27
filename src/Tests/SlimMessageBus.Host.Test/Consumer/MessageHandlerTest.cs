namespace SlimMessageBus.Host.Test;

public class MessageHandlerTest
{
    private readonly MessageBusMock busMock;
    private readonly Mock<IMessageScopeFactory> messageScopeFactoryMock;
    private readonly Mock<IMessageTypeResolver> messageTypeResolverMock;
    private readonly Mock<IMessageHeadersFactory> messageHeaderFactoryMock;
    private readonly Mock<IConsumerContext> consumerContextMock;
    private readonly Mock<IMessageTypeConsumerInvokerSettings> consumerInvokerMock;
    private readonly Mock<Func<object, object, Task>> consumerMethodMock;
    private readonly MessageHandler subject;

    public MessageHandlerTest()
    {
        busMock = new();
        messageScopeFactoryMock = new Mock<IMessageScopeFactory>();
        messageTypeResolverMock = new Mock<IMessageTypeResolver>();
        messageHeaderFactoryMock = new Mock<IMessageHeadersFactory>();
        consumerContextMock = new Mock<IConsumerContext>();
        consumerInvokerMock = new Mock<IMessageTypeConsumerInvokerSettings>();

        consumerMethodMock = new Mock<Func<object, object, Task>>();
        consumerInvokerMock.SetupGet(x => x.ConsumerMethod).Returns(consumerMethodMock.Object);

        subject = new MessageHandler(busMock.Bus, messageScopeFactoryMock.Object, messageTypeResolverMock.Object, messageHeaderFactoryMock.Object, new Host.Collections.RuntimeTypeCache(), busMock.Bus, "topic1");
    }

    [Fact]
    public async Task When_ExecuteConsumer_Given_Consumer_Then_ReturnsResponse()
    {
        // arrange
        var consumerMock = new Mock<SomeMessageConsumer>();

        var someMessage = new SomeMessage();

        consumerContextMock.SetupGet(x => x.Consumer).Returns(consumerMock.Object);
        consumerMethodMock.Setup(x => x(consumerMock.Object, someMessage)).Returns(Task.CompletedTask);

        // act
        var result = await subject.ExecuteConsumer(someMessage, consumerContextMock.Object, consumerInvokerMock.Object, null);

        // assert
        result.Should().BeNull();

        consumerMethodMock.Verify(x => x(consumerMock.Object, someMessage), Times.Once());
        consumerMethodMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task When_ExecuteConsumer_Given_ConsumerWithContext_Then_ConsumerContextSet()
    {
        // arrange
        var consumerMock = new Mock<IConsumerWithContext>();

        var someMessage = new SomeMessage();

        consumerContextMock.SetupGet(x => x.Consumer).Returns(consumerMock.Object);
        consumerMethodMock.Setup(x => x(consumerMock.Object, someMessage)).Returns(Task.CompletedTask);

        // act
        var result = await subject.ExecuteConsumer(someMessage, consumerContextMock.Object, consumerInvokerMock.Object, null);

        // assert
        consumerMock.VerifySet(x => { x.Context = consumerContextMock.Object; }, Times.Once());
        consumerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task When_ExecuteConsumer_Given_Handler_Then_ReturnsResponse()
    {
        // arrange
        var handlerMock = new Mock<SomeRequestMessageHandler>();

        var someRequest = new SomeRequest();
        var someResponse = new SomeResponse();

        consumerContextMock.SetupGet(x => x.Consumer).Returns(handlerMock.Object);
        consumerMethodMock.Setup(x => x(handlerMock.Object, someRequest)).Returns(Task.FromResult(someResponse));

        // act
        var result = await subject.ExecuteConsumer(someRequest, consumerContextMock.Object, consumerInvokerMock.Object, typeof(SomeResponse));

        // assert
        result.Should().BeSameAs(someResponse);

        consumerMethodMock.Verify(x => x(handlerMock.Object, someRequest), Times.Once());
        consumerMethodMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task When_ExecuteConsumer_Given_HandlerWithoutResponse_Then_ReturnsNull()
    {
        // arrange
        var handlerMock = new Mock<SomeRequestWithoutResponseHandler>();

        var someRequestWithoutResponse = new SomeRequestWithoutResponse();

        consumerContextMock.SetupGet(x => x.Consumer).Returns(handlerMock.Object);
        consumerMethodMock.Setup(x => x(handlerMock.Object, someRequestWithoutResponse)).Returns(Task.CompletedTask);

        // act
        var result = await subject.ExecuteConsumer(someRequestWithoutResponse, consumerContextMock.Object, consumerInvokerMock.Object, typeof(Void));

        // assert
        result.Should().BeNull();

        consumerMethodMock.Verify(x => x(handlerMock.Object, someRequestWithoutResponse), Times.Once());
        consumerMethodMock.VerifyNoOtherCalls();
    }
}