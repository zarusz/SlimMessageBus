namespace SlimMessageBus.Host.Sql;

public static class SqlProducerBuilderExtensions
{
    public static ProducerBuilder<T> DefaultQueue<T>(this ProducerBuilder<T> producerBuilder, string queue)
    {
        if (producerBuilder is null) throw new ArgumentNullException(nameof(producerBuilder));
        if (queue is null) throw new ArgumentNullException(nameof(queue));

        producerBuilder.DefaultTopic(queue);
        producerBuilder.ToQueue();
        return producerBuilder;
    }

    public static ProducerBuilder<T> ToTopic<T>(this ProducerBuilder<T> producerBuilder)
    {
        if (producerBuilder is null) throw new ArgumentNullException(nameof(producerBuilder));

        producerBuilder.Settings.PathKind = PathKind.Topic;
        return producerBuilder;
    }

    public static ProducerBuilder<T> ToQueue<T>(this ProducerBuilder<T> producerBuilder)
    {
        if (producerBuilder is null) throw new ArgumentNullException(nameof(producerBuilder));

        producerBuilder.Settings.PathKind = PathKind.Queue;
        return producerBuilder;
    }
}
