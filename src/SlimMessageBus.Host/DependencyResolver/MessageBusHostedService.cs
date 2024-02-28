namespace SlimMessageBus.Host;

using Microsoft.Extensions.Hosting;

/// <summary>
/// <see cref="IHostedService"/> responsible for starting message bus consumers.
/// </summary>
public class MessageBusHostedService(IConsumerControl bus, MessageBusSettings messageBusSettings) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (messageBusSettings.AutoStartConsumers)
        {
            await bus.Start();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => bus.Stop();
}