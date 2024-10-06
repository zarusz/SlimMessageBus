namespace SlimMessageBus.Host.Outbox.Services;

public interface IOutboxLockRenewalTimerFactory
{
    IOutboxLockRenewalTimer CreateRenewalTimer(TimeSpan lockDuration, TimeSpan interval, Action<Exception> lockLost, CancellationToken cancellationToken);
}
