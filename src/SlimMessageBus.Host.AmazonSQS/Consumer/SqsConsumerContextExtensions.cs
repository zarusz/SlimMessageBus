namespace SlimMessageBus.Host.AmazonSQS;

public static class SqsConsumerContextExtensions
{
    private const string MessageKey = "Sqs_Message";

    public static Message GetTransportMessage(this IConsumerContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        return context.GetPropertyOrDefault<Message>(MessageKey);
    }

    internal static void SetTransportMessage(this ConsumerContext context, Message message)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        context.Properties[MessageKey] = message;
    }
}
