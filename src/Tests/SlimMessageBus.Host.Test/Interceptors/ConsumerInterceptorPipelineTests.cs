namespace SlimMessageBus.Host.Test;

using SlimMessageBus.Host.Collections;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.Interceptor;

public class ConsumerInterceptorPipelineTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task When_Next_Given_RequestResponse_Then_InterceptorIsCalledAndTargetIsCalled(bool consumerInterceptorRegisterd, bool handlerInterceptorRegisterd)
    {
        // arrange
        var request = new SomeRequest();
        var response = new SomeResponse();
        var topic = "topic1";

        var consumerInterceptorMock = new Mock<IConsumerInterceptor<SomeRequest>>();
        consumerInterceptorMock
            .Setup(x => x.OnHandle(request, It.IsAny<Func<Task<object>>>(), It.IsAny<IConsumerContext>()))
            .Returns((SomeRequest message, Func<Task<object>> next, IConsumerContext context) => next());

        var consumerInterceptors = consumerInterceptorRegisterd ? new[] { consumerInterceptorMock.Object } : null;

        var requestHandlerInterceptorMock = new Mock<IRequestHandlerInterceptor<SomeRequest, SomeResponse>>();
        requestHandlerInterceptorMock
            .Setup(x => x.OnHandle(request, It.IsAny<Func<Task<SomeResponse>>>(), It.IsAny<IConsumerContext>()))
            .Returns((SomeRequest message, Func<Task<SomeResponse>> next, IConsumerContext context) => next());

        var handlerInterceptors = handlerInterceptorRegisterd ? new[] { requestHandlerInterceptorMock.Object } : null;

        var consumerSettings = new HandlerBuilder<SomeRequest, SomeResponse>(new MessageBusSettings())
            .Topic(topic)
            .WithHandler<IRequestHandler<SomeRequest, SomeResponse>>().ConsumerSettings;

        var handlerMock = new Mock<IRequestHandler<SomeRequest, SomeResponse>>();

        var context = new ConsumerContext
        {
            Consumer = handlerMock.Object,
            Headers = new Dictionary<string, object>(),
        };

        var messageHandlerMock = new Mock<IMessageHandler>();
        messageHandlerMock
            .Setup(x => x.ExecuteConsumer(request, context, consumerSettings, typeof(SomeResponse)))
            .Returns(Task.FromResult<object>(response));

        var runtimeTypeCache = new RuntimeTypeCache();

        var subject = new ConsumerInterceptorPipeline(runtimeTypeCache, messageHandlerMock.Object, request, typeof(SomeResponse), context, consumerSettings, consumerInterceptors: consumerInterceptors, handlerInterceptors: handlerInterceptors);

        // act
        var result = await subject.Next();

        // assert
        result.Should().BeSameAs(response);

        if (consumerInterceptorRegisterd)
        {
            consumerInterceptorMock.Verify(x => x.OnHandle(request, It.IsAny<Func<Task<object>>>(), It.IsAny<IConsumerContext>()), Times.Once);
        }
        consumerInterceptorMock.VerifyNoOtherCalls();

        if (handlerInterceptorRegisterd)
        {
            requestHandlerInterceptorMock.Verify(x => x.OnHandle(request, It.IsAny<Func<Task<SomeResponse>>>(), It.IsAny<IConsumerContext>()), Times.Once);
        }
        requestHandlerInterceptorMock.VerifyNoOtherCalls();

        messageHandlerMock.Verify(x => x.ExecuteConsumer(request, context, consumerSettings, typeof(SomeResponse)), Times.Once); // handler called once
        messageHandlerMock.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task When_Next_Given_RequestWithoutResponse_Then_InterceptorIsCalledAndTargetIsCalled(bool consumerInterceptorRegisterd, bool handlerInterceptorRegisterd)
    {
        // arrange
        var request = new SomeRequestWithoutResponse();
        var topic = "topic1";

        var consumerInterceptorMock = new Mock<IConsumerInterceptor<SomeRequestWithoutResponse>>();
        consumerInterceptorMock
            .Setup(x => x.OnHandle(request, It.IsAny<Func<Task<object>>>(), It.IsAny<IConsumerContext>()))
            .Returns((SomeRequestWithoutResponse message, Func<Task<object>> next, IConsumerContext context) => next());

        var consumerInterceptors = consumerInterceptorRegisterd ? new[] { consumerInterceptorMock.Object } : null;

        var requestHandlerInterceptorMock = new Mock<IRequestHandlerInterceptor<SomeRequestWithoutResponse, Void>>();
        requestHandlerInterceptorMock
            .Setup(x => x.OnHandle(request, It.IsAny<Func<Task<Void>>>(), It.IsAny<IConsumerContext>()))
            .Returns((SomeRequestWithoutResponse message, Func<Task<Void>> next, IConsumerContext context) => next());

        var handlerInterceptors = handlerInterceptorRegisterd ? new[] { requestHandlerInterceptorMock.Object } : null;

        var consumerSettings = new HandlerBuilder<SomeRequestWithoutResponse>(new MessageBusSettings())
            .Topic(topic)
            .WithHandler<IRequestHandler<SomeRequestWithoutResponse>>().ConsumerSettings;

        var handlerMock = new Mock<IRequestHandler<SomeRequestWithoutResponse>>();

        var context = new ConsumerContext
        {
            Consumer = handlerMock.Object,
            Headers = new Dictionary<string, object>(),
        };

        var messageHandlerMock = new Mock<IMessageHandler>();
        messageHandlerMock
            .Setup(x => x.ExecuteConsumer(request, context, consumerSettings, typeof(Void)))
            .Returns(Task.FromResult<object>(null));

        var runtimeTypeCache = new RuntimeTypeCache();

        var subject = new ConsumerInterceptorPipeline(runtimeTypeCache, messageHandlerMock.Object, request, typeof(Void), context, consumerSettings, consumerInterceptors: consumerInterceptors, handlerInterceptors: handlerInterceptors);

        // act
        var result = await subject.Next();

        // assert
        result.Should().BeSameAs(null);

        if (consumerInterceptorRegisterd)
        {
            consumerInterceptorMock.Verify(x => x.OnHandle(request, It.IsAny<Func<Task<object>>>(), It.IsAny<IConsumerContext>()), Times.Once);
        }
        consumerInterceptorMock.VerifyNoOtherCalls();

        if (handlerInterceptorRegisterd)
        {
            requestHandlerInterceptorMock.Verify(x => x.OnHandle(request, It.IsAny<Func<Task<Void>>>(), It.IsAny<IConsumerContext>()), Times.Once);
        }
        requestHandlerInterceptorMock.VerifyNoOtherCalls();

        messageHandlerMock.Verify(x => x.ExecuteConsumer(request, context, consumerSettings, typeof(Void)), Times.Once); // handler called once
        messageHandlerMock.VerifyNoOtherCalls();
    }
}
