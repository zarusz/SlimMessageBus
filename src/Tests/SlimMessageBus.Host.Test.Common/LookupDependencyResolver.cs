namespace SlimMessageBus.Host.Test.Common;

public class LookupDependencyResolver : IServiceProvider
{
    private readonly Func<Type, object> _provider;

    public LookupDependencyResolver(Func<Type, object> provider) => _provider = provider;

    public object? GetService(Type serviceType) => _provider(serviceType);
}