namespace SlimMessageBus.Host.Outbox.Services;

public class OutboxLockRenewalTimerFactory<TOutboxMessage, TOutboxMessageKey>(IServiceProvider serviceProvider)
    : IOutboxLockRenewalTimerFactory, IAsyncDisposable
    where TOutboxMessage : OutboxMessage<TOutboxMessageKey>
{
    private readonly IServiceScope _scope = serviceProvider.CreateScope();

    private bool _isDisposed = false;

    public IOutboxLockRenewalTimer CreateRenewalTimer(TimeSpan lockDuration, TimeSpan interval, Action<Exception> lockLost, CancellationToken cancellationToken)
        => ActivatorUtilities.CreateInstance<OutboxLockRenewalTimer<TOutboxMessage, TOutboxMessageKey>>(_scope.ServiceProvider, lockDuration, interval, lockLost, cancellationToken);

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
