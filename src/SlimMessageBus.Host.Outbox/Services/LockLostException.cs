namespace SlimMessageBus.Host.Outbox.Services;

public class LockLostException : Exception
{
    public LockLostException(string message)
        : base(message)
    {
    }

    public LockLostException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
