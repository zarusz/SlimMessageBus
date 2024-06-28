namespace SlimMessageBus.Host.Outbox.Services;

using System.Threading;

public interface IOutboxLockRenewalTimerFactory
{
    IOutboxLockRenewalTimer CreateRenewalTimer(TimeSpan lockDuration, TimeSpan interval, Action<Exception> lockLost, CancellationToken cancellationToken);
}
