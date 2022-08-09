namespace SlimMessageBus.Host.AzureEventHub.Test;

public record SomeMessage
{
}

public record SomeRequest : IRequestMessage<SomeResponse>
{
}

public record SomeResponse
{
}