namespace SlimMessageBus.Host.AzureServiceBus;

public record SubscriptionSqlRule
{
    public string Name { get; set; }
    public string SqlFilter { get; set; }
    public string SqlAction { get; set; }
}