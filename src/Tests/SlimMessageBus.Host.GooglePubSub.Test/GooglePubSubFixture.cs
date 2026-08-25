namespace SlimMessageBus.Host.GooglePubSub.Test;

public sealed class GooglePubSubFixture : IAsyncLifetime
{
    private readonly PubSubContainer _container = new PubSubBuilder(
        "gcr.io/google.com/cloudsdktool/google-cloud-cli:446.0.1-emulators").Build();

    public string EmulatorEndpoint => _container.GetEmulatorEndpoint();

    public Task InitializeAsync() => _container.StartAsync();

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(nameof(GooglePubSubCollection))]
public sealed class GooglePubSubCollection : ICollectionFixture<GooglePubSubFixture>;
