namespace SlimMessageBus;

public class ConsumerMessageBusException : MessageBusException
{
    public ConsumerMessageBusException()
    {
    }

    public ConsumerMessageBusException(string message) : base(message)
    {
    }

    public ConsumerMessageBusException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
