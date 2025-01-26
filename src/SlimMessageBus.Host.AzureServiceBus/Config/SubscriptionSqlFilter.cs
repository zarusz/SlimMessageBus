namespace SlimMessageBus.Host.AzureServiceBus;

public record SubscriptionSqlRule : SubscriptionRule
{
    public string SqlFilter { get; set; }
    public string SqlAction { get; set; }
}
