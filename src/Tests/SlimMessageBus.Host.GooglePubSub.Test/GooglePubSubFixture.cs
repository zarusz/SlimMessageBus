namespace SlimMessageBus.Host.GooglePubSub.Test;

public sealed class GooglePubSubFixture : IAsyncLifetime
{
    // Google removes image versions older than one year; keep this pin current.
    private readonly PubSubContainer _container = new PubSubBuilder(
        "gcr.io/google.com/cloudsdktool/google-cloud-cli:573.0.0-emulators").Build();

    public string EmulatorEndpoint => _container.GetEmulatorEndpoint();

    public Task InitializeAsync() => _container.StartAsync();

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(nameof(GooglePubSubCollection))]
public sealed class GooglePubSubCollection : ICollectionFixture<GooglePubSubFixture>;
