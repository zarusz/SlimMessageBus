namespace SlimMessageBus.Host.AsyncApi.Test;

public record Envelope<T>(Guid MessageId, T Message);
