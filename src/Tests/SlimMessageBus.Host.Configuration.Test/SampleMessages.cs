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

public record SomeDerivedRequest : SomeRequest
{
}

public record SomeRequestWithoutResponse : IRequest
{
}

public record SomeDerivedRequestWithoutResponse : SomeRequestWithoutResponse
{
}

public record SomeResponse
{
}

public class SomeMessageConsumer : IConsumer<SomeMessage>
{
    public Task OnHandle(SomeMessage message, CancellationToken cancellationToken) => throw new NotImplementedException(nameof(SomeMessage));
}

public class SomeRequestMessageHandler : IRequestHandler<SomeRequest, SomeResponse>
{
    public Task<SomeResponse> OnHandle(SomeRequest request, CancellationToken cancellationToken)
        => throw new NotImplementedException(nameof(SomeRequest));
}

public class SomeDerivedRequestMessageHandler : IRequestHandler<SomeDerivedRequest, SomeResponse>
{
    public Task<SomeResponse> OnHandle(SomeDerivedRequest request, CancellationToken cancellationToken)
        => throw new NotImplementedException(nameof(SomeDerivedRequest));
}

public class SomeRequestMessageHandlerOfContext : IRequestHandler<IConsumerContext<SomeRequest>, SomeResponse>
{
    public Task<SomeResponse> OnHandle(IConsumerContext<SomeRequest> request, CancellationToken cancellationToken)
        => throw new NotImplementedException(nameof(SomeRequest));
}

public class SomeDerivedRequestMessageHandlerOfContext : IRequestHandler<IConsumerContext<SomeDerivedRequest>, SomeResponse>
{
    public Task<SomeResponse> OnHandle(IConsumerContext<SomeDerivedRequest> request, CancellationToken cancellationToken)
        => throw new NotImplementedException(nameof(SomeDerivedRequest));
}

public class SomeRequestWithoutResponseHandler : IRequestHandler<SomeRequestWithoutResponse>
{
    public Task OnHandle(SomeRequestWithoutResponse request, CancellationToken cancellationToken)
        => throw new NotImplementedException(nameof(SomeRequestWithoutResponse));
}

public class SomeDerivedRequestWithoutResponseHandler : IRequestHandler<SomeDerivedRequestWithoutResponse>
{
    public Task OnHandle(SomeDerivedRequestWithoutResponse request, CancellationToken cancellationToken)
        => throw new NotImplementedException(nameof(SomeDerivedRequestWithoutResponse));
}

public class SomeRequestWithoutResponseHandlerOfContext : IRequestHandler<IConsumerContext<SomeRequestWithoutResponse>>
{
    public Task OnHandle(IConsumerContext<SomeRequestWithoutResponse> request, CancellationToken cancellationToken)
        => throw new NotImplementedException(nameof(SomeRequestWithoutResponse));
}

public class SomeDerivedRequestWithoutResponseHandlerOfContext : IRequestHandler<IConsumerContext<SomeDerivedRequestWithoutResponse>>
{
    public Task OnHandle(IConsumerContext<SomeDerivedRequestWithoutResponse> request, CancellationToken cancellationToken)
        => throw new NotImplementedException(nameof(SomeDerivedRequestWithoutResponse));
}
