namespace SlimMessageBus.Host;

using Microsoft.Extensions.Hosting;

/// <summary>
/// <see cref="IHostedService"/> responsible for starting message bus consumers.
/// </summary>
internal class MessageBusHostedService(IMasterMessageBus bus) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => bus.AutoStart(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => bus.Stop();
}