namespace SlimMessageBus;

public class SendMessageBusException : ProducerMessageBusException
{
    public SendMessageBusException()
    {
    }

    public SendMessageBusException(string message) : base(message)
    {
    }

    public SendMessageBusException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
