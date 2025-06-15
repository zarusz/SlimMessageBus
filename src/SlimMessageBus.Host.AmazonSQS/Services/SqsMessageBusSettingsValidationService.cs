namespace SlimMessageBus.Host.AmazonSQS;

using SlimMessageBus.Host.Services;

public class SqsMessageBusSettingsValidationService(MessageBusSettings settings) : DefaultMessageBusSettingsValidationService(settings)
{
    protected override void AssertConsumer(ConsumerSettings consumerSettings)
    {
        if (consumerSettings == null) throw new ArgumentNullException(nameof(consumerSettings));

        // First check SQS validations as it will be more relevant, then the base validations
        var queue = consumerSettings.GetOrDefault(SqsProperties.UnderlyingQueue, Settings);
        if (string.IsNullOrEmpty(queue))
        {
            ThrowConsumerFieldNotSet(consumerSettings, nameof(SqsConsumerBuilderExtensions.Queue));
        }

        base.AssertConsumer(consumerSettings);
    }
}