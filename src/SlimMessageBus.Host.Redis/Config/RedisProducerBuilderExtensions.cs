namespace SlimMessageBus.Host.Redis;

public static class RedisProducerBuilderExtensions
{
    public static ProducerBuilder<T> DefaultQueue<T>(this ProducerBuilder<T> producerBuilder, string queue)
    {
        if (producerBuilder is null) throw new ArgumentNullException(nameof(producerBuilder));
        if (queue is null) throw new ArgumentNullException(nameof(queue));

        producerBuilder.DefaultTopic(queue);
        producerBuilder.ToQueue();
        return producerBuilder;
    }

    /// <summary>
    /// The topic parameter name in <see cref="IPublishBus.Publish{TMessage}"/> should be treated as a topic name
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="producerBuilder"></param>
    /// <returns></returns>
    public static ProducerBuilder<T> ToTopic<T>(this ProducerBuilder<T> producerBuilder)
    {
        if (producerBuilder is null) throw new ArgumentNullException(nameof(producerBuilder));

        producerBuilder.Settings.PathKind = PathKind.Topic;
        return producerBuilder;
    }

    /// <summary>
    /// The topic parameter name in <see cref="IPublishBus.Publish{TMessage}"/> should be treated as a queue name (it will be a Redis list)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="producerBuilder"></param>
    /// <returns></returns>
    public static ProducerBuilder<T> ToQueue<T>(this ProducerBuilder<T> producerBuilder)
    {
        if (producerBuilder is null) throw new ArgumentNullException(nameof(producerBuilder));

        producerBuilder.Settings.PathKind = PathKind.Queue;
        return producerBuilder;
    }
}