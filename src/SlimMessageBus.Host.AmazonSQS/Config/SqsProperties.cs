namespace SlimMessageBus.Host.AmazonSQS;

static internal class SqsProperties
{
    // producer
    static readonly internal ProviderExtensionProperty<bool> EnableFifo = new("Sqs_EnableFifo");
    static readonly internal ProviderExtensionProperty<MessageGroupIdProvider<object>> MessageGroupId = new("Sqs_MessageGroupId");
    static readonly internal ProviderExtensionProperty<MessageDeduplicationIdProvider<object>> MessageDeduplicationId = new("Sqs_MessageDeduplicationId");
    static readonly internal ProviderExtensionProperty<Dictionary<string, string>> Tags = new("Sqs_Tags");
    static readonly internal ProviderExtensionProperty<Dictionary<string, string>> Attributes = new("Sqs_Attributes");
    static readonly internal ProviderExtensionProperty<string> Policy = new("Sqs_Policy");

    // consumer
    static readonly internal ProviderExtensionProperty<int?> MaxMessages = new("Sqs_MaxMessages");
    static readonly internal ProviderExtensionProperty<int?> VisibilityTimeout = new("Sqs_VisibilityTimeout");
    static readonly internal ProviderExtensionProperty<int?> WaitTimeSeconds = new("Sqs_WaitTimeSeconds");
    static readonly internal ProviderExtensionProperty<string[]> MessageAttributes = new("Sqs_MessageAttributes");
    static readonly internal ProviderExtensionProperty<string> SubscribeToTopic = new("Sqs_SubscribeToTopic");
    static readonly internal ProviderExtensionProperty<string> SubscribeToTopicFilterPolicy = new("Sqs_SubscribeToTopic_FilterPolicy");
    static readonly internal ProviderExtensionProperty<string> UnderlyingQueue = new("Sqs_UnderlyingQueue");
}