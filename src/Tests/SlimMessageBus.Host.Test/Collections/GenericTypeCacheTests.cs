namespace SlimMessageBus.Host.Test.Collections;

using SlimMessageBus.Host.Collections;
using SlimMessageBus.Host.DependencyResolver;
using SlimMessageBus.Host.Interceptor;

public class GenericTypeCacheTests
{
    private readonly Mock<IConsumerInterceptor<SomeMessage>> consumerInterceptorMock;
    private readonly Mock<IDependencyResolver> scopeMock;
    private readonly GenericTypeCache<Func<object, object, object, object, Task>> subject;

    public GenericTypeCacheTests()
    {
        consumerInterceptorMock = new Mock<IConsumerInterceptor<SomeMessage>>();

        scopeMock = new Mock<IDependencyResolver>();
        scopeMock.Setup(x => x.Resolve(typeof(IEnumerable<IConsumerInterceptor<SomeMessage>>))).Returns(() => new[] { consumerInterceptorMock.Object });

        subject = new GenericTypeCache<Func<object, object, object, object, Task>>(typeof(IConsumerInterceptor<>), nameof(IConsumerInterceptor<object>.OnHandle), mt => typeof(Task), mt => new[] { typeof(Func<Task<object>>), typeof(IConsumerContext) });
    }

    [Fact]
    public void When_ResolveAll_Given_OneRegistrationExists_Then_ReturnsThatRegistration()
    {
        // arrange

        // act
        var interceptors = subject.ResolveAll(scopeMock.Object, typeof(SomeMessage));

        // assert
        scopeMock.Verify(x => x.Resolve(typeof(IEnumerable<IConsumerInterceptor<SomeMessage>>)), Times.Once);
        scopeMock.VerifyNoOtherCalls();

        interceptors.Should().HaveCount(1);
        interceptors.Should().Contain(consumerInterceptorMock.Object);
    }

    [Fact]
    public void When_ResolveAll_Given_NoRegistrations_Then_ReturnsNull()
    {
        // arrange
        scopeMock.Setup(x => x.Resolve(typeof(IEnumerable<IConsumerInterceptor<SomeMessage>>))).Returns(() => Enumerable.Empty<object>());

        // act
        var interceptors = subject.ResolveAll(scopeMock.Object, typeof(SomeMessage));

        // assert
        scopeMock.Verify(x => x.Resolve(typeof(IEnumerable<IConsumerInterceptor<SomeMessage>>)), Times.Once);
        scopeMock.VerifyNoOtherCalls();

        interceptors.Should().BeNull();
    }

    [Fact]
    public async Task When_Get_Then_ReturnsValidMethodAndInterceptorGenericType()
    {
        // arrange
        var message = new SomeMessage();
        Func<Task<object>> next = () => Task.FromResult<object>(null);
        var headers = new Dictionary<string, object>();
        var consumer = new object();
        var consumerContext = new ConsumerContext();

        consumerInterceptorMock.Setup(x => x.OnHandle(message, next, consumerContext)).Returns(Task.FromResult<object>(null));

        // act
        var interceptorTypeFunc = subject[typeof(SomeMessage)];

        var task = interceptorTypeFunc(consumerInterceptorMock.Object, message, next, consumerContext);
        await task;

        // assert
        consumerInterceptorMock.Verify(x => x.OnHandle(message, next, consumerContext), Times.Once);
        consumerInterceptorMock.VerifyNoOtherCalls();
    }
}
