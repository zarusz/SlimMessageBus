namespace SlimMessageBus.Host.Test.Collections;

using SlimMessageBus.Host.Collections;
using SlimMessageBus.Host.Interceptor;

public class RuntimeTypeCacheTests
{
    private readonly RuntimeTypeCache _subject;

    public RuntimeTypeCacheTests()
    {
        _subject = new RuntimeTypeCache();
    }

    [Theory]
    [InlineData(typeof(bool), typeof(int), false)]
    [InlineData(typeof(SomeMessageConsumer), typeof(IConsumer<SomeMessage>), true)]
    [InlineData(typeof(SomeMessageConsumer), typeof(IRequestHandler<SomeRequest, SomeResponse>), false)]
    [InlineData(typeof(IConsumer<SomeMessage>), typeof(SomeMessageConsumer), false)]
    public void When_IsAssignableFrom(Type from, Type to, bool expectedResult)
    {
        // arrange

        // act
        var result = _subject.IsAssignableFrom(from, to);

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
        var interceptorTypeFunc = _subject.HandlerInterceptorType[(typeof(SomeRequest), typeof(SomeResponse))];

        var task = (Task<SomeResponse>)interceptorTypeFunc(requestHandlerInterceptorMock.Object, request, next, consumerContext);
        await task;
        var actualResponse = taskOfType.GetResult(task);

        // assert
        requestHandlerInterceptorMock.Verify(x => x.OnHandle(request, next, consumerContext), Times.Once);
        requestHandlerInterceptorMock.VerifyNoOtherCalls();

        actualResponse.Should().BeSameAs(response);
    }

    [Theory]
    [InlineData(typeof(IConsumer<>), typeof(bool), typeof(IConsumer<bool>))]
    [InlineData(typeof(IConsumer<>), typeof(SomeMessage), typeof(IConsumer<SomeMessage>))]
    public void When_GetClosedGenericType(Type openGenericType, Type genericParameterType, Type expectedResult)
    {
        // arrange

        // act
        var result = _subject.GetClosedGenericType(openGenericType, genericParameterType);

        // assert
        result.Should().Be(expectedResult);
    }

    public static TheoryData<object, bool> Data => new()
    {
        { (new SomeMessage[] { new() }).Concat([new SomeMessage()]), true },

        { new List<SomeMessage> { new(), new() }, true },

        { new SomeMessage[] { new(), new() }, true},

        { new HashSet<SomeMessage> { new(), new() }, true },

        { new object(), false },
    };

    [Theory]
    [MemberData(nameof(Data))]
    public void Given_ObjectThatIsCollection_When_Then(object collection, bool isCollection)
    {
        // arrange

        // actr
        var collectionInfo = _subject.GetCollectionTypeInfo(collection.GetType());

        // assert
        if (isCollection)
        {
            collectionInfo.Should().NotBeNull();
            collectionInfo.ItemType.Should().Be(typeof(SomeMessage));
            var col = collectionInfo.ToCollection(collection);
            col.Should().NotBeNull();
            col.Should().BeSameAs((IEnumerable<object>)collection);
        }
        else
        {
            collectionInfo.Should().BeNull();
        }
    }
}
