namespace SlimMessageBus.Host.AmazonSQS;

public abstract class AbstractClientProvider<TClient>(TClient client) : IDisposable
    where TClient : IDisposable
{
    private bool _disposedValue;

    public TClient Client => client;

    public virtual Task EnsureClientAuthenticated() => Task.CompletedTask;

    #region Dispose Pattern

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                client?.Dispose();
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion

}
