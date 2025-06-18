namespace SlimMessageBus.Host.AmazonSQS;

public static class SqsConsumerBuilderExtensions
{
    public static TConsumerBuilder Queue<TConsumerBuilder>(this TConsumerBuilder consumerBuilder, string queue)
        where TConsumerBuilder : AbstractConsumerBuilder<TConsumerBuilder>
    {
        if (consumerBuilder is null) throw new ArgumentNullException(nameof(consumerBuilder));
        if (queue is null) throw new ArgumentNullException(nameof(queue));

        consumerBuilder.ConsumerSettings.PathKind = PathKind.Queue;
        consumerBuilder.ConsumerSettings.Path = queue;
        return consumerBuilder;
    }

    /// <summary>
    /// Specifies the visibility timeout for the message. Default is 30 seconds.
    /// <see cref="ReceiveMessageRequest.VisibilityTimeout"/> for more information.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="consumerBuilder"></param>
    /// <param name="visibilityTimeoutSeconds"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static ConsumerBuilder<T> VisibilityTimeout<T>(this ConsumerBuilder<T> consumerBuilder, int visibilityTimeoutSeconds)
    {
        if (consumerBuilder is null) throw new ArgumentNullException(nameof(consumerBuilder));
        if (visibilityTimeoutSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(visibilityTimeoutSeconds));

        SqsProperties.VisibilityTimeout.Set(consumerBuilder.Settings, visibilityTimeoutSeconds);
        return consumerBuilder;
    }

    /// <summary>
    /// Specifies the maximum number of messages to receive in a single poll. Default is 1, maximum is 10.
    /// <see cref="ReceiveMessageRequest.MaxNumberOfMessages"/> for more information.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="consumerBuilder"></param>
    /// <param name="maxMessages"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static ConsumerBuilder<T> MaxMessages<T>(this ConsumerBuilder<T> consumerBuilder, int maxMessages)
    {
        if (consumerBuilder is null) throw new ArgumentNullException(nameof(consumerBuilder));
        if (maxMessages <= 0 || maxMessages > 10) throw new ArgumentOutOfRangeException(nameof(maxMessages));

        SqsProperties.MaxMessages.Set(consumerBuilder.Settings, maxMessages);
        return consumerBuilder;
    }

    /// <summary>
    /// Specifies the message attribute names to fetch. Default is "All".
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="consumerBuilder"></param>
    /// <param name="messageAttributeNames"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static ConsumerBuilder<T> FetchMessageAttributes<T>(this ConsumerBuilder<T> consumerBuilder, params string[] messageAttributeNames)
    {
        if (consumerBuilder is null) throw new ArgumentNullException(nameof(consumerBuilder));

        SqsProperties.MessageAttributes.Set(consumerBuilder.Settings, messageAttributeNames);
        return consumerBuilder;
    }
}