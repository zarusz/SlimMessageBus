namespace SlimMessageBus.Host.AmazonSQS;

public static class MessageBusBuilderExtensions
{
    public static MessageBusBuilder WithProviderAmazonSQS(this MessageBusBuilder mbb, Action<SqsMessageBusSettings> configure)
    {
        if (mbb is null) throw new ArgumentNullException(nameof(mbb));
        if (configure == null) throw new ArgumentNullException(nameof(configure));

        var providerSettings = new SqsMessageBusSettings();
        configure(providerSettings);

        mbb.PostConfigurationActions.Add((services) =>
        {
            services.TryAddSingleton(providerSettings.ClientProviderFactory);
        });

        return mbb.WithProvider(settings => new SqsMessageBus(settings, providerSettings));
    }
}