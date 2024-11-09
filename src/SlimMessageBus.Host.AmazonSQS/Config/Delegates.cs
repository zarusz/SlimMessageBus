namespace SlimMessageBus.Host.AmazonSQS;

/// <summary>
/// Allows to convert an message and headers into a Message Group Id (used for FIFO queues to group messages together and ensure order of processing).
/// </summary>
/// <typeparam name="T"></typeparam>
/// <param name="message"></param>
/// <param name="headers"></param>
/// <returns></returns>
public delegate string MessageGroupIdProvider<T>(T message, IDictionary<string, object> headers);

/// <summary>
/// Allows to convert an message and headers into a Message Deduplication Id (Amazon SQS performs deduplication within a 5-minute window).
/// </summary>
/// <typeparam name="T"></typeparam>
/// <param name="message"></param>
/// <param name="headers"></param>
/// <returns></returns>
public delegate string MessageDeduplicationIdProvider<T>(T message, IDictionary<string, object> headers);
