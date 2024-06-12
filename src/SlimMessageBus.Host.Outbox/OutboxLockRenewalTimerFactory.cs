namespace SlimMessageBus.Host.Outbox;

public class OutboxLockRenewalTimerFactory : IOutboxLockRenewalTimerFactory, IAsyncDisposable, IDisposable
{
    private bool _isDisposed = false;
    private IServiceScope _scope;

    public OutboxLockRenewalTimerFactory(IServiceProvider serviceProvider)
    {
        _scope = serviceProvider.CreateScope();
    }

    public IOutboxLockRenewalTimer CreateRenewalTimer(TimeSpan lockDuration, TimeSpan interval, Action<Exception> lockLost, CancellationToken cancellationToken)
    {
        return (OutboxLockRenewalTimer)ActivatorUtilities.CreateInstance(_scope.ServiceProvider, typeof(OutboxLockRenewalTimer), [lockDuration, interval, lockLost, cancellationToken]);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        _scope.Dispose();
        GC.SuppressFinalize(this);
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
