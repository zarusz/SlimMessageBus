namespace SlimMessageBus.Host.AmazonSQS;

[Flags]
public enum SqsMessageBusModes
{
    Sqs = 1,
    Sns = 2,
    All = Sqs | Sns
}
