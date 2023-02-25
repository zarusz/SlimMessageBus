namespace SlimMessageBus.Host.AzureEventHub.Test;

public record SomeMessage
{
}

public record SomeRequest : IRequest<SomeResponse>
{
}

public record SomeResponse
{
}