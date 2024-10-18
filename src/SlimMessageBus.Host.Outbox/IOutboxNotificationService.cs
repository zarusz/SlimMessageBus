namespace SlimMessageBus.Host.Outbox;

public interface IOutboxNotificationService
{
    /// <summary>
    /// Notify outbox service that a message is waiting to be published.
    /// </summary>
    void Notify();
}
