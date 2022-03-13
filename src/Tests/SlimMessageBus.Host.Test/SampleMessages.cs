namespace SlimMessageBus.Host.Test
{
    using System;
    using System.Threading.Tasks;

    public class SomeMessage
    {
    }

    public class SomeRequest : IRequestMessage<SomeResponse>
    {
    }

    public class SomeResponse
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

    public class SomeDerivedMessage : SomeMessage
    {
    }

    public class SomeDerived2Message : SomeMessage
    {
    }
}