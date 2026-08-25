namespace SlimMessageBus.Host.GooglePubSub;

public static class MessageBusBuilderExtensions
{
    /// <summary>
    /// Configures Google Cloud Pub/Sub as the transport provider.
    /// </summary>
    public static MessageBusBuilder WithProviderGooglePubSub(this MessageBusBuilder mbb, Action<GooglePubSubMessageBusSettings> configure)
    {
        if (mbb is null) throw new ArgumentNullException(nameof(mbb));
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        var providerSettings = new GooglePubSubMessageBusSettings();
        configure(providerSettings);

        return mbb.WithProvider(settings => new GooglePubSubMessageBus(settings, providerSettings));
    }
}
