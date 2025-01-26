namespace Sample.CircuitBreaker.HealthCheck;

public class IntermittentMessagePublisher(ILogger<IntermittentMessagePublisher> logger, IMessageBus messageBus)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var a = Random.Shared.Next(10);
            var b = Random.Shared.Next(10);

            logger.LogInformation("Emitting {A} +- {B} = ?", a, b);

            await Task.WhenAll(
                messageBus.Publish(new Add(a, b), cancellationToken: stoppingToken),
                messageBus.Publish(new Subtract(a, b), cancellationToken: stoppingToken),
                Task.Delay(1000, stoppingToken));
        }
    }
}
