namespace SlimMessageBus.Host;

using Microsoft.Extensions.Hosting;

/// <summary>
/// <see cref="IHostedService"/> responsible for starting message bus consumers.
/// </summary>
public class MessageBusHostedService : IHostedService
{
    private readonly IConsumerControl _bus;

    public MessageBusHostedService(IConsumerControl bus) => _bus = bus;

    public Task StartAsync(CancellationToken cancellationToken) => _bus.Start();

    public Task StopAsync(CancellationToken cancellationToken) => _bus.Stop();
}