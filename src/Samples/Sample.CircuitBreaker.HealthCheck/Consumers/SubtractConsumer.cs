namespace Sample.CircuitBreaker.HealthCheck.Consumers;

public class SubtractConsumer(ILogger<SubtractConsumer> logger) : IConsumer<Subtract>
{
    public Task OnHandle(Subtract message, CancellationToken cancellationToken)
    {
        logger.LogInformation("{A} - {B} = {C}", message.A, message.B, message.A - message.B);
        return Task.CompletedTask;
    }
}
