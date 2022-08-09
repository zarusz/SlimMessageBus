namespace SlimMessageBus.Host.DependencyResolver;

/// <summary>
/// Builder class to fluently configure the <see cref="MessageBus.SetProvider"/> provider
/// </summary>
public class MessageBusCurrentProviderBuilder
{
    private Func<IMessageBus> provider;

    protected MessageBusCurrentProviderBuilder()
    {
    }

    public static MessageBusCurrentProviderBuilder Create() => new MessageBusCurrentProviderBuilder();

    public void SetProvider(Func<IMessageBus> provider) => this.provider = provider;

    public MessageBusCurrentProviderBuilder FromSingleton(IMessageBus bus)
    {
        SetProvider(() => bus);
        return this;
    }

    public MessageBusCurrentProviderBuilder From(IDependencyResolver dependencyResolver)
    {
        SetProvider(() => (IMessageBus)dependencyResolver.Resolve(typeof(IMessageBus)));
        return this;
    }

    public Func<IMessageBus> Build() => provider;
}
