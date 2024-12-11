namespace SlimMessageBus.Host.AzureServiceBus;

public abstract record SubscriptionRule
{
    public string Name { get; set; }
}
