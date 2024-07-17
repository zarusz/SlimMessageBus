namespace Sample.CircuitBreaker.HealthCheck.Consumers;

public class SubtractConsumer : IConsumer<Subtract>
{
    private readonly ILogger<SubtractConsumer> _logger;

    public SubtractConsumer(ILogger<SubtractConsumer> logger)
    {
        _logger = logger;
    }

    public Task OnHandle(Subtract message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("{A} - {B} = {C}", message.a, message.b, message.a - message.b);
        return Task.CompletedTask;
    }
}
