namespace SlimMessageBus.Host.RabbitMQ;

public static class RabbitMqMessageBusBuilderExtensions
{
    /// <summary>
    /// Adds RabbitMQ transport provider for this bus.
    /// </summary>
    /// <param name="mbb"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static MessageBusBuilder WithProviderRabbitMQ(this MessageBusBuilder mbb, Action<RabbitMqMessageBusSettings> configure)
    {
        if (mbb == null) throw new ArgumentNullException(nameof(mbb));
        if (configure == null) throw new ArgumentNullException(nameof(configure));

        var providerSettings = mbb.Settings.GetOrCreate(RabbitMqProperties.ProvderSettings, () => new RabbitMqMessageBusSettings());
        
        configure(providerSettings);

        return mbb.WithProvider(settings => new RabbitMqMessageBus(settings, providerSettings));
    }
}