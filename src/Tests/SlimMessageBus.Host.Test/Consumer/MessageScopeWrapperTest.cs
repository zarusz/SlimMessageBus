namespace SlimMessageBus.Host.Test.Consumer;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using SlimMessageBus.Host.Consumer;

public class MessageScopeWrapperTest
{
    private readonly Mock<object> messageMock;
    private readonly Mock<IServiceScopeFactory> serviceScopeFactoryMock;
    private readonly Mock<IServiceProvider> serviceProviderMock;
    private readonly Mock<IServiceProvider> scopeServiceProviderMock;
    private readonly Mock<IServiceScope> scopeMock;

    public MessageScopeWrapperTest()
    {
        messageMock = new Mock<object>();

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
        var currentServiceProvider = currentScopeExists ? new Mock<IServiceProvider>() : null;

        // erase current message scope
        MessageScope.Current = currentServiceProvider?.Object;

        // act
        var subject = new MessageScopeWrapper(NullLogger.Instance, serviceProviderMock.Object, createScope, messageMock.Object);

        // assert
        if (currentScopeExists)
        {
            MessageScope.Current.Should().BeSameAs(currentServiceProvider.Object);
        }
        else
        {
            if (createScope)
            {
                MessageScope.Current.Should().BeSameAs(scopeServiceProviderMock.Object);
            }
            else
            {
                MessageScope.Current.Should().BeNull();
            }
        }

        if (!currentScopeExists)
        {
            if (createScope)
            {
                scopeMock.VerifyGet(x => x.ServiceProvider, Times.Once());
                serviceScopeFactoryMock.Verify(x => x.CreateScope(), Times.Once());
                serviceProviderMock.Verify(x => x.GetService(typeof(IServiceScopeFactory)), Times.Once());
            }
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
        var currentServiceProvider = currentScopeExists ? new Mock<IServiceProvider>() : null;

        // erase current message scope
        MessageScope.Current = currentServiceProvider?.Object;

        var subject = new MessageScopeWrapper(NullLogger.Instance, serviceProviderMock.Object, createScope, messageMock.Object);

        // act
        await subject.DisposeAsync().ConfigureAwait(false);

        // assert
        if (currentScopeExists)
        {
            MessageScope.Current.Should().BeSameAs(currentServiceProvider.Object);
        }
        else
        {
            MessageScope.Current.Should().BeNull();
        }

        if (!currentScopeExists)
        {
            if (createScope)
            {
                scopeMock.VerifyGet(x => x.ServiceProvider, Times.Once());
                scopeMock.Verify(x => x.Dispose(), Times.Once());

                serviceScopeFactoryMock.Verify(x => x.CreateScope(), Times.Once());
                serviceProviderMock.Verify(x => x.GetService(typeof(IServiceScopeFactory)), Times.Once());
            }
        }

        scopeMock.VerifyNoOtherCalls();
        serviceScopeFactoryMock.VerifyNoOtherCalls();
        serviceProviderMock.VerifyNoOtherCalls();
    }
}