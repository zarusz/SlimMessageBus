namespace SlimMessageBus.Host.AmazonSQS;

public class StaticCredentialsSqsClientProvider : ISqsClientProvider, IDisposable
{
    private bool _disposedValue;

    private readonly AmazonSQSClient _client;

    public StaticCredentialsSqsClientProvider(AmazonSQSConfig sqsConfig, AWSCredentials credentials)
        => _client = new AmazonSQSClient(credentials, sqsConfig);

    #region ISqsClientProvider

    public AmazonSQSClient Client => _client;

    public Task EnsureClientAuthenticated() => Task.CompletedTask;

    #endregion

    #region Dispose Pattern

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _client?.Dispose();
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


