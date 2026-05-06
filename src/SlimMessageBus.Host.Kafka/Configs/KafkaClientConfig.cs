namespace SlimMessageBus.Host.Kafka;

public class KafkaClientConfig<T>
where T : ClientConfig
{
    public T ConfluentConfig { get; set; }

    public ILogger Logger { get; set; }
}