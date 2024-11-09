namespace SlimMessageBus.Host.AmazonSQS;

public static class SqsProducerBuilderExtensions
{
    public static ProducerBuilder<T> DefaultQueue<T>(this ProducerBuilder<T> producerBuilder, string queue)
    {
        if (producerBuilder is null) throw new ArgumentNullException(nameof(producerBuilder));
        if (queue is null) throw new ArgumentNullException(nameof(queue));

        producerBuilder.ToQueue();
        return producerBuilder.DefaultPath(queue);
    }

    /// <summary>
    /// The path parameter name in <see cref="IPublishBus.Publish{TMessage}"/> should be treated as a SNS topic name
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
    /// The path parameter name in <see cref="IPublishBus.Publish{TMessage}"/> should be treated as a SQS queue name
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

    /// <summary>
    /// Enables FIFO support for the queue when it will be provisioned.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="producerBuilder"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static ProducerBuilder<T> EnableFifo<T>(this ProducerBuilder<T> producerBuilder, Action<SqsProducerFifoBuilder<T>> builder = null)
    {
        if (producerBuilder is null) throw new ArgumentNullException(nameof(producerBuilder));

        SqsProperties.EnableFifo.Set(producerBuilder.Settings, true);

        builder?.Invoke(new SqsProducerFifoBuilder<T>(producerBuilder.Settings));

        return producerBuilder;
    }

    /// <summary>
    /// Sets the tags for the queue when it will be provisioned.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="producerBuilder"></param>
    /// <param name="tags"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static ProducerBuilder<T> Tags<T>(this ProducerBuilder<T> producerBuilder, Dictionary<string, string> tags)
    {
        if (producerBuilder is null) throw new ArgumentNullException(nameof(producerBuilder));
        if (tags is null) throw new ArgumentNullException(nameof(tags));

        SqsProperties.Tags.Set(producerBuilder.Settings, tags);

        return producerBuilder;
    }

    /// <summary>
    /// Sets the attributes for the queue when it will be provisioned. See the available names in <see cref="QueueAttributeName"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="producerBuilder"></param>
    /// <param name="attributes"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static ProducerBuilder<T> Attributes<T>(this ProducerBuilder<T> producerBuilder, Dictionary<string, string> attributes)
    {
        if (producerBuilder is null) throw new ArgumentNullException(nameof(producerBuilder));
        if (attributes is null) throw new ArgumentNullException(nameof(attributes));

        SqsProperties.Attributes.Set(producerBuilder.Settings, attributes);

        return producerBuilder;
    }

    /// <summary>
    /// Sets the <see cref="QueueAttributeName.VisibilityTimeout"/> for the queue when it will be provisioned.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="producerBuilder"></param>
    /// <param name="visibilityTimeoutSeconds"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static ProducerBuilder<T> VisibilityTimeout<T>(this ProducerBuilder<T> producerBuilder, int visibilityTimeoutSeconds)
    {
        if (producerBuilder is null) throw new ArgumentNullException(nameof(producerBuilder));

        SqsProperties.VisibilityTimeout.Set(producerBuilder.Settings, visibilityTimeoutSeconds);

        return producerBuilder;
    }

    /// <summary>
    /// Sets the <see cref="QueueAttributeName.Policy"/> for the queue when it will be provisioned.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="producerBuilder"></param>
    /// <param name="policy"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static ProducerBuilder<T> Policy<T>(this ProducerBuilder<T> producerBuilder, string policy)
    {
        if (producerBuilder is null) throw new ArgumentNullException(nameof(producerBuilder));
        if (policy is null) throw new ArgumentNullException(nameof(policy));

        SqsProperties.Policy.Set(producerBuilder.Settings, policy);

        return producerBuilder;
    }
}