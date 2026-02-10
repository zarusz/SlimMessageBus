namespace SlimMessageBus.Host.Nats;

public static class NatsProducerBuilderExtensions
{
    public static ProducerBuilder<T> DefaultQueue<T>(this ProducerBuilder<T> natsProducerBuilder, string natsQueue)
    {
        if (natsProducerBuilder is null) throw new ArgumentNullException(nameof(natsProducerBuilder));
        if (natsQueue is null) throw new ArgumentNullException(nameof(natsQueue));

        natsProducerBuilder.DefaultTopic(natsQueue);
        natsProducerBuilder.ToQueue();
        return natsProducerBuilder;
    }

    /// <summary>
    /// The topic parameter name in <see cref="IPublishBus.Publish{TMessage}"/> should be treated as a topic name
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="natsProducerBuilder"></param>
    /// <returns></returns>
    public static ProducerBuilder<T> ToTopic<T>(this ProducerBuilder<T> natsProducerBuilder)
    {
        if (natsProducerBuilder is null) throw new ArgumentNullException(nameof(natsProducerBuilder));

        natsProducerBuilder.Settings.PathKind = PathKind.Topic;
        return natsProducerBuilder;
    }

    /// <summary>
    /// The topic parameter name in <see cref="IPublishBus.Publish{TMessage}"/> should be treated as a queue group name
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="natsProducerBuilder"></param>
    /// <returns></returns>
    public static ProducerBuilder<T> ToQueue<T>(this ProducerBuilder<T> natsProducerBuilder)
    {
        if (natsProducerBuilder is null) throw new ArgumentNullException(nameof(natsProducerBuilder));

        natsProducerBuilder.Settings.PathKind = PathKind.Queue;
        return natsProducerBuilder;
    }
}