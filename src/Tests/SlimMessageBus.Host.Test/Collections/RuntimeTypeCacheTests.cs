namespace SlimMessageBus.Host.Test.Collections;

using SlimMessageBus.Host.Collections;
using SlimMessageBus.Host.Interceptor;

public class RuntimeTypeCacheTests
{
    [Theory]
    [InlineData(typeof(bool), typeof(int), false)]
    [InlineData(typeof(SomeMessageConsumer), typeof(IConsumer<SomeMessage>), true)]
    [InlineData(typeof(SomeMessageConsumer), typeof(IRequestHandler<SomeRequest, SomeResponse>), false)]
    [InlineData(typeof(IConsumer<SomeMessage>), typeof(SomeMessageConsumer), false)]
    public void IsAssignableFromWorks(Type from, Type to, bool expectedResult)
    {
        // arrange
        var subject = new RuntimeTypeCache();

        // act
        var result = subject.IsAssignableFrom(from, to);

        // assert
        result.Should().Be(expectedResult);
    }

    [Fact]
    public async Task When_HandlerInterceptorType_Then_ReturnsValidMethodAndInterceptorGenericType()
    {
        // arrange
        var taskOfType = new TaskOfTypeCache(typeof(SomeResponse));

        var requestHandlerInterceptorMock = new Mock<IRequestHandlerInterceptor<SomeRequest, SomeResponse>>();

        var scopeMock = new Mock<IServiceProvider>();
        scopeMock
            .Setup(x => x.GetService(typeof(IEnumerable<IRequestHandlerInterceptor<SomeRequest, SomeResponse>>)))
            .Returns(() => new[] { requestHandlerInterceptorMock.Object });

        var subject = new RuntimeTypeCache();

        var request = new SomeRequest();
        var response = new SomeResponse();

        //Func<object> next = () => Task.FromResult(response);
        Func<Task<SomeResponse>> next = () => Task.FromResult(response);

        var headers = new Dictionary<string, object>();
        var consumer = new object();
        var consumerContext = new ConsumerContext();

        requestHandlerInterceptorMock
            .Setup(x => x.OnHandle(request, It.IsAny<Func<Task<SomeResponse>>>(), consumerContext))
            .Returns((SomeRequest r, Func<Task<SomeResponse>> n, IConsumerContext c) => n());

        // act
        var interceptorTypeFunc = subject.HandlerInterceptorType[(typeof(SomeRequest), typeof(SomeResponse))];

        var task = (Task<SomeResponse>)interceptorTypeFunc(requestHandlerInterceptorMock.Object, request, next, consumerContext);
        await task;
        var actualResponse = taskOfType.GetResult(task);

        // assert
        requestHandlerInterceptorMock.Verify(x => x.OnHandle(request, next, consumerContext), Times.Once);
        requestHandlerInterceptorMock.VerifyNoOtherCalls();

        actualResponse.Should().BeSameAs(response);
    }
}
