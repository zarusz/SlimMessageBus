namespace SlimMessageBus.Host.AzureServiceBus
{
    public class SubscriptionFactoryParams
    {
        public string Path { get; set; }
        public string SubscriptionName { get; set; }

        public SubscriptionFactoryParams(string path, string subscriptionName)
        {
            Path = path;
            SubscriptionName = subscriptionName;
        }
    }
}