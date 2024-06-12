namespace SlimMessageBus.Host.Outbox;

public interface IOutboxLockRenewalTimer : IDisposable
{
    bool Active { get; }
    public string InstanceId { get; }
    public TimeSpan LockDuration { get; }
    public TimeSpan RenewalInterval { get; }

    void Start();
    void Stop();
}