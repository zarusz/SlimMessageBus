namespace SlimMessageBus.Host.AmazonSQS;

using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;

public class TemporaryCredentialsSqsClientProvider : ISqsClientProvider, IDisposable
{
    private bool _disposedValue;

    private readonly AmazonSQSConfig _sqsConfig;
    private readonly string _roleArn;
    private readonly string _roleSessionName;

    private readonly AmazonSecurityTokenServiceClient _stsClient;
    private readonly Timer _timer;
    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);

    private AmazonSQSClient _client;
    private DateTime _clientCredentialsExpiry;

    public TemporaryCredentialsSqsClientProvider(AmazonSQSConfig sqsConfig, string roleArn, string roleSessionName)
    {
        _stsClient = new AmazonSecurityTokenServiceClient();
        _sqsConfig = sqsConfig;
        _roleArn = roleArn;
        _roleSessionName = roleSessionName;
        _timer = new Timer(state => _ = EnsureClientAuthenticated(), null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }

    #region ISqsClientProvider

    public AmazonSQSClient Client => _client;

    public async Task EnsureClientAuthenticated()
    {
        if (_client == null || DateTime.UtcNow >= _clientCredentialsExpiry)
        {
            await _semaphoreSlim.WaitAsync();
            try
            {
                var oldClient = _client;
                (_client, _clientCredentialsExpiry) = await RefreshCredentialsAsync();
                oldClient?.Dispose();
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }
    }

    #endregion

    private async Task<(AmazonSQSClient Client, DateTime ClientExpiry)> RefreshCredentialsAsync()
    {
        var assumeRoleRequest = new AssumeRoleRequest
        {
            RoleArn = _roleArn,
            RoleSessionName = _roleSessionName
        };

        var assumeRoleResponse = await _stsClient.AssumeRoleAsync(assumeRoleRequest);

        var temporaryCredentials = new SessionAWSCredentials(
            assumeRoleResponse.Credentials.AccessKeyId,
            assumeRoleResponse.Credentials.SecretAccessKey,
            assumeRoleResponse.Credentials.SessionToken
        );

        var clientCredentialsExpiry = assumeRoleResponse.Credentials.Expiration.AddMinutes(-5); // Renew 5 mins before expiry

        var client = new AmazonSQSClient(temporaryCredentials, _sqsConfig);
        return (client, clientCredentialsExpiry);
    }

    #region Dispose Pattern

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _client?.Dispose();
                _stsClient?.Dispose();
                _timer?.Dispose();
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


