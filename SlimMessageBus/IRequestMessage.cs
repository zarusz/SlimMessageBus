namespace SlimMessageBus
{
    public interface IRequestMessage : IHasRequestId
    {
        
    }

    public interface IRequestMessageWithResponse<TResponse> : IRequestMessage
        where TResponse: IResponseMessage
    {
    }
}