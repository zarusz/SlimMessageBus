namespace SlimMessageBus.Host.Consumer;

public interface IMessageScope : IAsyncDisposable
{
    IServiceProvider ServiceProvider { get; }
}
