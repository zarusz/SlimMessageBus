namespace SlimMessageBus.Host.AmazonSQS;

public class SqsProducerFifoBuilder<T>(ProducerSettings producerSettings)
{
    /// <summary>
    /// Used for FIFO queues to provide a message group id in order to group messages together and ensure order of processing.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="provider"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public SqsProducerFifoBuilder<T> GroupId(MessageGroupIdProvider<T> provider)
    {
        if (provider is null) throw new ArgumentNullException(nameof(provider));

        SqsProperties.MessageGroupId.Set(producerSettings, (message, headers) => provider((T)message, headers));
        return this;
    }

    /// <summary>
    /// Used to specfiy a message deduplication id for the message. This is used to prevent duplicate messages from being sent (Amazon SQS performs deduplication within a 5-minute window).
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="provider"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public SqsProducerFifoBuilder<T> DeduplicationId(MessageDeduplicationIdProvider<T> provider)
    {
        if (provider is null) throw new ArgumentNullException(nameof(provider));

        SqsProperties.MessageDeduplicationId.Set(producerSettings, (message, headers) => provider((T)message, headers));
        return this;
    }
}