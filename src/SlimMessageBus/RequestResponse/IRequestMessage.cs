namespace SlimMessageBus
{
    /// <summary>
    /// Marker interface for request messages.
    /// </summary>
    /// <typeparam name="TResponse">The response message type associated with the request</typeparam>
    public interface IRequestMessage<TResponse>
    {
    }
}