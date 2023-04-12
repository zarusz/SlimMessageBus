namespace SlimMessageBus.Host;

using Microsoft.Extensions.Hosting;

/// <summary>
/// <see cref="IHostedService"/> responsible for starting message bus consumers.
/// </summary>
public class MessageBusHostedService : IHostedService
{
    private readonly IConsumerControl _bus;
    private readonly MessageBusSettings _messageBusSettings;

    public MessageBusHostedService(IConsumerControl bus, MessageBusSettings messageBusSettings)
    {
        _bus = bus;
        _messageBusSettings = messageBusSettings;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_messageBusSettings.AutoStartConsumers)
        {
            await _bus.Start();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => _bus.Stop();
}