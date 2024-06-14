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

public interface ICustomConsumer<T>
{
    Task HandleAMessage(T message, CancellationToken cancellationToken);

    Task HandleAMessageWithAContext(T message, IConsumerContext consumerContext, CancellationToken cancellationToken);

    Task MethodThatHasParamatersThatCannotBeSatisfied(T message, DateTimeOffset dateTimeOffset, CancellationToken cancellationToken);
}

public class RequestA : IRequest<ResponseA>
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
}

public class ResponseA
{
    public string Id { get; set; }
}

public class RequestB : IRequest<ResponseB> { }

public class ResponseB { }