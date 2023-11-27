namespace SlimMessageBus.Host.AzureServiceBus;

using Azure.Messaging.ServiceBus.Administration;

public static class AsbProducerBuilderExtensions
{
    public static ProducerBuilder<T> DefaultQueue<T>(this ProducerBuilder<T> producerBuilder, string queue)
    {
        if (producerBuilder is null) throw new ArgumentNullException(nameof(producerBuilder));
        if (queue is null) throw new ArgumentNullException(nameof(queue));

        producerBuilder.DefaultPath(queue);
        producerBuilder.ToQueue();
        return producerBuilder;
    }

    /// <summary>
    /// The topic parameter name in <see cref="IPublishBus.Publish{TMessage}"/> should be treated as a Azure ServiceBus topic name
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
    /// The topic parameter name in <see cref="IPublishBus.Publish{TMessage}"/> should be treated as a Azure ServiceBus queue name
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
    /// Allows to set additional properties to the native <see cref="ServiceBusMessage"/> when producing the <see cref="T"/> message.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="producerBuilder"></param>
    /// <param name="modifier"></param>
    /// <returns></returns>
    public static ProducerBuilder<T> WithModifier<T>(this ProducerBuilder<T> producerBuilder, AsbMessageModifier<T> modifier)
    {
        if (producerBuilder is null) throw new ArgumentNullException(nameof(producerBuilder));
        if (modifier is null) throw new ArgumentNullException(nameof(modifier));

        producerBuilder.Settings.SetMessageModifier((e, m) => modifier((T)e, m));
        return producerBuilder;
    }

    /// <summary>
    /// <see cref="CreateQueueOptions"/> when the ASB queue does not exist and needs to be created
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="producerBuilder"></param>
    /// <returns></returns>
    public static ProducerBuilder<T> CreateQueueOptions<T>(this ProducerBuilder<T> producerBuilder, Action<CreateQueueOptions> action)
    {
        if (producerBuilder is null) throw new ArgumentNullException(nameof(producerBuilder));
        if (action is null) throw new ArgumentNullException(nameof(action));

        producerBuilder.Settings.SetQueueOptions(action);
        return producerBuilder;
    }

    /// <summary>
    /// <see cref="CreateTopicOptions"/> when the ASB topic does not exist and needs to be created
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="producerBuilder"></param>
    /// <returns></returns>
    public static ProducerBuilder<T> CreateTopicOptions<T>(this ProducerBuilder<T> producerBuilder, Action<CreateTopicOptions> action)
    {
        if (producerBuilder is null) throw new ArgumentNullException(nameof(producerBuilder));
        if (action is null) throw new ArgumentNullException(nameof(action));

        producerBuilder.Settings.SetTopicOptions(action);
        return producerBuilder;
    }
}
