namespace Sample.CircuitBreaker.HealthCheck.Consumers;

public class AddConsumer(ILogger<AddConsumer> logger) : IConsumer<Add>
{
    public Task OnHandle(Add message, CancellationToken cancellationToken)
    {
        logger.LogInformation("{A} + {B} = {C}", message.A, message.B, message.A + message.B);
        return Task.CompletedTask;
    }
}
