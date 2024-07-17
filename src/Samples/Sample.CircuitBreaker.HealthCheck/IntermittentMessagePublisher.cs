namespace Sample.CircuitBreaker.HealthCheck;
public class IntermittentMessagePublisher : BackgroundService
{
    private readonly ILogger _logger;
    private readonly IMessageBus _messageBus;

    public IntermittentMessagePublisher(ILogger<IntermittentMessagePublisher> logger, IMessageBus messageBus)
    {
        _logger = logger;
        _messageBus = messageBus;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var a = Random.Shared.Next(10);
            var b = Random.Shared.Next(10);

            //_logger.LogInformation("Emitting {A} +- {B} = ?", a, b);

            await Task.WhenAll(
                _messageBus.Publish(new Add(a, b)),
                _messageBus.Publish(new Subtract(a, b)),
                Task.Delay(1000, stoppingToken));
        }
    }
}
