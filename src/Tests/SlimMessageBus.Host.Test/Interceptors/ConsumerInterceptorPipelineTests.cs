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
    public async Task When_Next_Then_InterceptorIsCalledAndTargetIsCalled(bool consumerInterceptorRegisterd, bool handlerInterceptorRegisterd)
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
}
