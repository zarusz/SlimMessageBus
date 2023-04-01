namespace SlimMessageBus.Host.Kafka;

public static class MessageBusBuilderExtensions
{
    public static MessageBusBuilder WithProviderKafka(this MessageBusBuilder mbb, Action<KafkaMessageBusSettings> configure)
    {
        if (mbb == null) throw new ArgumentNullException(nameof(mbb));
        if (configure == null) throw new ArgumentNullException(nameof(configure));

        var providerSettings = new KafkaMessageBusSettings();
        configure(providerSettings);

        return mbb.WithProvider(settings => new KafkaMessageBus(settings, providerSettings));
    }
}