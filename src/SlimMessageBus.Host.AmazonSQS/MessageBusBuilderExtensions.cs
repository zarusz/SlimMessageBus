namespace SlimMessageBus.Host.AmazonSQS;

public static class MessageBusBuilderExtensions
{
    /// <summary>
    /// The bus will use the Amazon SQS / SNS transport.
    /// </summary>
    /// <param name="mbb"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static MessageBusBuilder WithProviderAmazonSQS(this MessageBusBuilder mbb, Action<SqsMessageBusSettings> configure)
    {
        if (mbb is null) throw new ArgumentNullException(nameof(mbb));
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        var providerSettings = new SqsMessageBusSettings();
        configure(providerSettings);

        mbb.PostConfigurationActions.Add((services) =>
        {
            // Register the client wrappers
            services.TryAddSingleton(providerSettings.SqsClientProviderFactory);
            services.TryAddSingleton(providerSettings.SnsClientProviderFactory);
        });

        return mbb.WithProvider(settings => new SqsMessageBus(settings, providerSettings));
    }
}