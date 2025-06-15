namespace SlimMessageBus.Host.AmazonSQS;

internal class SnsEnvelope
{
    public string Type { get; set; }
    public string MessageId { get; set; }
    public string TopicArn { get; set; }
    public string Message { get; set; }
    public string Timestamp { get; set; }
    public Dictionary<string, SnsMessageAttribute> MessageAttributes { get; set; }
}
