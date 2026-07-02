namespace SlimMessageBus.Host.PostgreSql;

public static class PostgreSqlProducerBuilderExtensions
{
    public static ProducerBuilder<T> DefaultQueue<T>(this ProducerBuilder<T> producerBuilder, string queue)
        => RelationalBuilderExtensions.DefaultQueue(producerBuilder, queue);

    public static ProducerBuilder<T> ToTopic<T>(this ProducerBuilder<T> producerBuilder)
        => RelationalBuilderExtensions.ToTopic(producerBuilder);

    public static ProducerBuilder<T> ToQueue<T>(this ProducerBuilder<T> producerBuilder)
        => RelationalBuilderExtensions.ToQueue(producerBuilder);
}
