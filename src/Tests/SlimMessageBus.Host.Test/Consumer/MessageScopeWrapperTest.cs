namespace SlimMessageBus.Host.Test.Consumer;

using Microsoft.Extensions.DependencyInjection;

using SlimMessageBus.Host.Consumer;

public class MessageScopeWrapperTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NestedDispatchRestoresProviderAndContextAfterFailure(bool createScope)
    {
        var outerProvider = serviceProviderMock.Object;
        var outerContext = new ConsumerContext { Path = "outer" };
        var innerContext = new ConsumerContext { Path = "inner" };
        using var outerFrame = MessageScope.BeginDispatch(outerProvider, outerContext);

        Func<Task> dispatch = async () =>
        {
            await using var innerScope = new MessageScopeWrapper(serviceProviderMock.Object, createScope);
            using var innerFrame = MessageScope.BeginDispatch(innerScope, innerContext);
            await Task.Yield();

            MessageScope.Current.Should().BeSameAs(innerScope.ServiceProvider);
            MessageScope.CurrentConsumerContext.Should().BeSameAs(innerContext);
            AmbientConsumerContext.Instance.Path.Should().Be("inner");

            throw new InvalidOperationException("Dispatch failed");
        };

        await dispatch.Should().ThrowAsync<InvalidOperationException>();

        MessageScope.Current.Should().BeSameAs(outerProvider);
        MessageScope.CurrentConsumerContext.Should().BeSameAs(outerContext);
    }

    [Fact]
    public async Task ChangingTheProviderDoesNotKeepTheContextAliveAfterDispatch()
    {
        var context = new ConsumerContext { Path = "message" };
        ExecutionContext capturedContext;
        await using (var scope = new MessageScopeWrapper(scopeServiceProviderMock.Object, createMessageScope: false))
        {
            using var dispatch = MessageScope.BeginDispatch(scope, context);
            MessageScope.Current = serviceProviderMock.Object;
            MessageScope.Current.Should().BeSameAs(serviceProviderMock.Object);
            AmbientConsumerContext.Instance.Path.Should().Be("message");
            capturedContext = ExecutionContext.Capture();
        }

        ExecutionContext.Run(capturedContext, _ =>
        {
            var readPath = () => AmbientConsumerContext.Instance.Path;
            readPath.Should().Throw<InvalidOperationException>();
        }, null);
    }

    [Fact]
    public async Task DispatchUsesTheScopeProviderWhenTheAmbientProviderWasOverridden()
    {
        await using var scope = new MessageScopeWrapper(scopeServiceProviderMock.Object, createMessageScope: false);
        MessageScope.Current = serviceProviderMock.Object;
        var context = new ConsumerContext { Path = "message" };

        using (MessageScope.BeginDispatch(scope, context))
        {
            MessageScope.Current.Should().BeSameAs(scopeServiceProviderMock.Object);
            MessageScope.CurrentConsumerContext.Should().BeSameAs(context);
        }

        MessageScope.Current.Should().BeSameAs(serviceProviderMock.Object);
    }

    private readonly Mock<IServiceScopeFactory> serviceScopeFactoryMock;
    private readonly Mock<IServiceProvider> serviceProviderMock;
    private readonly Mock<IServiceProvider> scopeServiceProviderMock;
    private readonly Mock<IServiceScope> scopeMock;

    public MessageScopeWrapperTest()
    {

        scopeServiceProviderMock = new Mock<IServiceProvider>();

        scopeMock = new Mock<IServiceScope>();
        scopeMock.SetupGet(x => x.ServiceProvider).Returns(scopeServiceProviderMock.Object);

        serviceScopeFactoryMock = new Mock<IServiceScopeFactory>();
        serviceScopeFactoryMock.Setup(x => x.CreateScope()).Returns(scopeMock.Object);

        serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(x => x.GetService(typeof(IServiceScopeFactory))).Returns(serviceScopeFactoryMock.Object);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void When_Create_Given_CurrentMessageScope_And_CreateScopeValue_Then_CreatesANewScopeAndAssignsCurrentMessageContextOrReuses(bool currentScopeExists, bool createScope)
    {
        // arrange
        MessageScope.Current = currentScopeExists
            ? serviceProviderMock.Object
            : null;

        // act
        var subject = new MessageScopeWrapper(serviceProviderMock.Object, createScope);

        // assert
        MessageScope.Current.Should().BeSameAs(createScope ? scopeServiceProviderMock.Object : serviceProviderMock.Object);

        if (createScope)
        {
            scopeMock.VerifyGet(x => x.ServiceProvider, Times.Once());
            serviceScopeFactoryMock.Verify(x => x.CreateScope(), Times.Once());
            serviceProviderMock.Verify(x => x.GetService(typeof(IServiceScopeFactory)), Times.Once());
        }

        scopeMock.VerifyNoOtherCalls();
        serviceScopeFactoryMock.VerifyNoOtherCalls();
        serviceProviderMock.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task When_Dispose_Given_CurrentMessageScope_And_CreateScopeValue_Then_CreatesANewScopeAndAssignsCurrentMessageContextOrReuses(bool currentScopeExists, bool createScope)
    {
        // arrange
        var currentServiceProvider = currentScopeExists
            ? serviceProviderMock.Object
            : null;

        // erase current message scope
        MessageScope.Current = currentServiceProvider;

        var subject = new MessageScopeWrapper(serviceProviderMock.Object, createScope);

        // act
        await subject.DisposeAsync();

        // assert
        MessageScope.Current.Should().BeSameAs(currentServiceProvider);

        if (createScope)
        {
            scopeMock.VerifyGet(x => x.ServiceProvider, Times.Once());
            scopeMock.Verify(x => x.Dispose(), Times.Once());

            serviceScopeFactoryMock.Verify(x => x.CreateScope(), Times.Once());
            serviceProviderMock.Verify(x => x.GetService(typeof(IServiceScopeFactory)), Times.Once());
        }

        scopeMock.VerifyNoOtherCalls();
        serviceScopeFactoryMock.VerifyNoOtherCalls();
        serviceProviderMock.VerifyNoOtherCalls();
    }
}
