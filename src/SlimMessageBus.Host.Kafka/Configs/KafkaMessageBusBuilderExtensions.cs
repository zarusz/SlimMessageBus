namespace SlimMessageBus.Host.Kafka;

public static class KafkaMessageBusBuilderExtensions
{
    public static MessageBusBuilder WithProviderKafka(this MessageBusBuilder mbb, KafkaMessageBusSettings kafkaSettings)
    {
        return mbb.WithProvider(settings => new KafkaMessageBus(settings, kafkaSettings));
    }
}