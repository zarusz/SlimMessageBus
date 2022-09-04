namespace SlimMessageBus.Host.Test;

public interface ISomeMessageMarkerInterface
{
}

public record SomeMessage : ISomeMessageMarkerInterface
{
}

public record SomeRequest : IRequestMessage<SomeResponse>, ISomeMessageMarkerInterface
{
}

public record SomeResponse
{
}

public class SomeMessageConsumer : IConsumer<SomeMessage>
{
    public Task OnHandle(SomeMessage message, string name)
        => throw new NotImplementedException();
}

public class SomeRequestMessageHandler : IRequestHandler<SomeRequest, SomeResponse>
{
    public Task<SomeResponse> OnHandle(SomeRequest request, string name)
        => throw new NotImplementedException(nameof(SomeRequest));
}

public record SomeDerivedMessage : SomeMessage
{
}

public record SomeDerived2Message : SomeMessage
{
}

public record Envelope<T>
{
    public T Body { get; set; }
}