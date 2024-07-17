namespace Sample.CircuitBreaker.HealthCheck.Consumers;

public class AddConsumer : IConsumer<Add>
{
    private readonly ILogger<AddConsumer> _logger;

    public AddConsumer(ILogger<AddConsumer> logger)
    {
        _logger = logger;
    }

    public Task OnHandle(Add message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("{A} + {B} = {C}", message.a, message.b, message.a + message.b);
        return Task.CompletedTask;
    }
}
