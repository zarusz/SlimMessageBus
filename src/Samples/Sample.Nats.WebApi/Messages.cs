namespace Sample.Nats.WebApi;

public record PingMessage(int Counter, Guid Value);
public record QueueMessage(int Counter, Guid Value);

