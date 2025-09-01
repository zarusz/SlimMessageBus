namespace SlimMessageBus.Host.Services;

public interface IMessageBusSettingsValidationService
{
    void AssertSettings();
}

public class DefaultMessageBusSettingsValidationService(MessageBusSettings settings) : IMessageBusSettingsValidationService
{
    public MessageBusSettings Settings { get; } = settings;

    public virtual void AssertSettings()
    {
        AssertDepencendyResolverSettings();
        AssertProducers();
        foreach (var producerSettings in Settings.Producers)
        {
            AssertProducer(producerSettings);
        }
        AssertConsumers();
        foreach (var consumerSettings in Settings.Consumers)
        {
            AssertConsumer(consumerSettings);
        }
        AssertRequestResponseSettings();
    }

    protected virtual void ThrowProducerFieldNotSet(ProducerSettings producerSettings, string fieldName, string message = "is not set")
        => throw new ConfigurationMessageBusException(Settings, $"Producer ({producerSettings.MessageType.Name}): The {fieldName} {message}");

    protected virtual void ThrowProducerFieldNotSet(ProducerSettings producerSettings, string[] fieldNames, string message = "is not set")
        => ThrowProducerFieldNotSet(producerSettings, string.Join(" or ", fieldNames), message);

    protected internal virtual void AssertProducer(ProducerSettings producerSettings)
    {
        if (producerSettings == null) throw new ArgumentNullException(nameof(producerSettings));

        if (string.IsNullOrEmpty(producerSettings.DefaultPath))
        {
            ThrowProducerFieldNotSet(producerSettings, nameof(producerSettings.DefaultPath));
        }
    }

    protected internal virtual void AssertProducers()
    {
        var duplicateMessageTypeProducer = Settings.Producers.GroupBy(x => x.MessageType).Where(x => x.Count() > 1).Select(x => x.FirstOrDefault()).FirstOrDefault();
        if (duplicateMessageTypeProducer != null)
        {
            throw new ConfigurationMessageBusException(Settings, $"The produced message type {duplicateMessageTypeProducer.MessageType} was declared more than once (check the {nameof(MessageBusBuilder.Produce)} configuration)");
        }
    }

    protected virtual void ThrowConsumerFieldNotSet(ConsumerSettings consumerSettings, string fieldName)
        => throw new ConfigurationMessageBusException($"Consumer ({consumerSettings.MessageType?.Name}): The {fieldName} is not set");

    protected virtual void ThrowConsumerFieldNotSet(ConsumerSettings consumerSettings, string[] fieldNames)
        => ThrowConsumerFieldNotSet(consumerSettings, string.Join(" or ", fieldNames));

    protected internal virtual void AssertConsumers()
    {
    }

    protected internal virtual void AssertConsumer(ConsumerSettings consumerSettings)
    {
        if (consumerSettings == null) throw new ArgumentNullException(nameof(consumerSettings));

        if (string.IsNullOrEmpty(consumerSettings.Path))
        {
            ThrowConsumerFieldNotSet(consumerSettings, nameof(consumerSettings.Path));
        }
        if (consumerSettings.MessageType == null)
        {
            ThrowConsumerFieldNotSet(consumerSettings, nameof(consumerSettings.MessageType));
        }
        if (consumerSettings.ConsumerType == null)
        {
            ThrowConsumerFieldNotSet(consumerSettings, nameof(consumerSettings.ConsumerType));
        }
        if (consumerSettings.ConsumerMethod == null)
        {
            ThrowConsumerFieldNotSet(consumerSettings, nameof(consumerSettings.ConsumerMethod));
        }
    }

    protected virtual void ThrowRequestResponseFieldNotSet(string fieldName, string message = "is not set")
        => throw new ConfigurationMessageBusException(Settings, $"RequestResponse: The {fieldName} {message}");

    protected internal virtual void AssertDepencendyResolverSettings()
    {
        Assert.IsNotNull(Settings.ServiceProvider,
            () => new ConfigurationMessageBusException(Settings, $"The {nameof(MessageBusSettings)}.{nameof(MessageBusSettings.ServiceProvider)} is not set"));
    }

    protected internal virtual void AssertRequestResponseSettings()
    {
        if (Settings.RequestResponse != null)
        {
            if (string.IsNullOrEmpty(Settings.RequestResponse.Path))
            {
                ThrowRequestResponseFieldNotSet(nameof(Settings.RequestResponse.Path));
            }
        }
    }

    protected virtual void ThrowFieldNotSet(string fieldName) => throw new ConfigurationMessageBusException(Settings, $"The {fieldName} is not set");
}

public class DefaultMessageBusSettingsValidationService<TProviderSettings>(MessageBusSettings settings, TProviderSettings providerSettings)
    : DefaultMessageBusSettingsValidationService(settings)
{
    public TProviderSettings ProviderSettings { get; } = providerSettings;
}