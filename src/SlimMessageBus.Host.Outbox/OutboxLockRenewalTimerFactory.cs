namespace SlimMessageBus.Host.Outbox;

public class OutboxLockRenewalTimerFactory : IOutboxLockRenewalTimerFactory, IAsyncDisposable
{
    private readonly IServiceScope _scope;

    private bool _isDisposed = false;

    public OutboxLockRenewalTimerFactory(IServiceProvider serviceProvider)
    {
        _scope = serviceProvider.CreateScope();
    }

    public IOutboxLockRenewalTimer CreateRenewalTimer(TimeSpan lockDuration, TimeSpan interval, Action<Exception> lockLost, CancellationToken cancellationToken)
    {
        return (OutboxLockRenewalTimer)ActivatorUtilities.CreateInstance(_scope.ServiceProvider, typeof(OutboxLockRenewalTimer), lockDuration, interval, lockLost, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        if (_scope is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else
        {
            _scope.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
