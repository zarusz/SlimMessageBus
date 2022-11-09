namespace SlimMessageBus;

public class ProducerMessageBusException : MessageBusException
{
    public ProducerMessageBusException()
    {
    }

    public ProducerMessageBusException(string message) : base(message)
    {
    }

    public ProducerMessageBusException(string message, Exception innerException) : base(message, innerException)
    {
    }
}