namespace SlimMessageBus.Host.AzureServiceBus;

public static class AsbProperties
{
    public static readonly string SubscriptionNameKey = "Asb_SubscriptionName";
    public static readonly string MaxAutoLockRenewalDurationKey = "Asb_MaxAutoLockRenewalDuration";
    public static readonly string SubQueueKey = "Asb_SubQueue";
    public static readonly string PrefetchCountKey = "Asb_PrefetchCount";
    public static readonly string EnableSessionKey = "Asb_SessionEnabled";
    public static readonly string SessionIdleTimeoutKey = "Asb_SessionIdleTimeout";
    public static readonly string MaxConcurrentSessionsKey = "Asb_MaxConcurrentSessions";
    public static readonly string RulesKey = "Asb_Rules";

    // Producer
    static readonly internal ProviderExtensionProperty<AsbMessageModifier<object>> MessageModifier = new("Asb_MessageModifier");

    // Producer and Consumer
    static readonly internal ProviderExtensionProperty<Action<CreateQueueOptions>> CreateQueueOptions = new("Asb_CreateQueueOptions");
    static readonly internal ProviderExtensionProperty<Action<CreateTopicOptions>> CreateTopicOptions = new("Asb_CreateTopicOptions");

    // Consumer
    static readonly internal ProviderExtensionProperty<Action<CreateSubscriptionOptions>> CreateSubscriptionOptions = new("Asb_CreateSubscriptionOptions");
}
