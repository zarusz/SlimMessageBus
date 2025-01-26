namespace SlimMessageBus.Host.AzureServiceBus;

public record SubscriptionCorrelationRule : SubscriptionRule
{
    public string CorrelationId { get; set; }
    public string MessageId { get; set; }
    public string To { get; set; }
    public string ReplyTo { get; set; }
    public string Subject { get; set; }
    public string SessionId { get; set; }
    public string ReplyToSessionId { get; set; }
    public string ContentType { get; set; }
    public IDictionary<string, object> ApplicationProperties { get; set; }
}