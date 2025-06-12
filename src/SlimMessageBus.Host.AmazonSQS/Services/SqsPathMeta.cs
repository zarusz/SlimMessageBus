namespace SlimMessageBus.Host.AmazonSQS;

internal class SqsPathMeta(string url, string arn, PathKind pathKind)
{
    public string Url { get; } = url;
    public string Arn { get; } = arn;
    public PathKind PathKind { get; } = pathKind;
}
