namespace SlimMessageBus;

public class RequestHandlerFaultedMessageBusException : ConsumerMessageBusException
{
    public RequestHandlerFaultedMessageBusException()
    {
    }

    public RequestHandlerFaultedMessageBusException(string message) : base(message)
    {
    }

    public RequestHandlerFaultedMessageBusException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
