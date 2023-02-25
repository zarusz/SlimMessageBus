namespace SlimMessageBus.Host.Test;

public interface ISomeMessageMarkerInterface
{
}

public record SomeMessage : ISomeMessageMarkerInterface
{
}

public record SomeRequest : IRequest<SomeResponse>, ISomeMessageMarkerInterface
{
}

public record SomeRequestWithoutResponse : IRequest
{
}


public record SomeResponse
{
}

public class SomeMessageConsumer : IConsumer<SomeMessage>
{
    public Task OnHandle(SomeMessage message)
        => throw new NotImplementedException();
}

public class SomeRequestMessageHandler : IRequestHandler<SomeRequest, SomeResponse>
{
    public Task<SomeResponse> OnHandle(SomeRequest request)
        => throw new NotImplementedException(nameof(SomeRequest));
}

public class SomeRequestWithoutResponseHandler : IRequestHandler<SomeRequestWithoutResponse>
{
    public Task OnHandle(SomeRequestWithoutResponse request)
        => throw new NotImplementedException(nameof(SomeRequestWithoutResponse));
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