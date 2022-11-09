namespace SlimMessageBus;

public class PublishMessageBusException : ProducerMessageBusException
{
    public PublishMessageBusException()
    {
    }

    public PublishMessageBusException(string message) : base(message)
    {
    }

    public PublishMessageBusException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
