namespace SlimMessageBus.Host.Test.Common;

using Microsoft.Extensions.DependencyInjection;

public class ServiceProviderMock
{
    public Mock<IServiceProvider> ProviderMock { get; } = new();
    public Mock<IServiceScopeFactory> ScopeFactoryMock { get; } = new();
    public Action<Mock<IServiceProvider>, Mock<IServiceScope>>? OnScopeCreated { get; set; }

    public ServiceProviderMock()
    {
        ScopeFactoryMock.Setup(x => x.CreateScope()).Returns(() =>
        {
            var scopeProviderMock = new Mock<IServiceProvider>();
            
            var scopeMock = new Mock<IServiceScope>();
            scopeMock.SetupGet(x => x.ServiceProvider).Returns(() => scopeProviderMock.Object);

            OnScopeCreated?.Invoke(scopeProviderMock, scopeMock);

            return scopeMock.Object;
        });
        ProviderMock.Setup(x => x.GetService(typeof(IServiceScopeFactory))).Returns(() => ScopeFactoryMock.Object);
    }
}

