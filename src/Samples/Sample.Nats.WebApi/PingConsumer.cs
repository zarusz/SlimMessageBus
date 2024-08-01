namespace Sample.Nats.WebApi;

using SlimMessageBus;

public class PingConsumer(ILogger<PingConsumer> logger) : IConsumer<PingMessage>, IConsumerWithContext
{
    private readonly ILogger _logger = logger;

    public IConsumerContext Context { get; set; }

    public Task OnHandle(PingMessage message)
    {
        _logger.LogInformation("Got message {Counter} on topic {Path}", message.Counter, Context.Path);
        return Task.CompletedTask;
    }
}