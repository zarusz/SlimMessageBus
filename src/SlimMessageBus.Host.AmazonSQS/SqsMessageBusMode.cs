namespace SlimMessageBus.Host.AmazonSQS;

[Flags]
public enum SqsMessageBusMode
{
    Sqs = 1,
    Sns = 2,
    All = Sqs | Sns
}
