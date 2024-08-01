namespace SlimMessageBus.Host.Nats.Config;

public static class MessageBusBuilderExtensions
{
    public static MessageBusBuilder WithProviderNats(this MessageBusBuilder mbb, Action<NatsMessageBusSettings> configure)
    {
        if (mbb == null) throw new ArgumentNullException(nameof(mbb));
        if (configure == null) throw new ArgumentNullException(nameof(configure));

        var providerSettings = new NatsMessageBusSettings();
        configure(providerSettings);

        return mbb.WithProvider(settings => new NatsMessageBus(settings, providerSettings));
    }
}