namespace SlimMessageBus.Host;

public static class MessageBusSettingsExtensions
{
    public static IMessageSerializerProvider GetSerializerProvider(this MessageBusSettings settings, IServiceProvider serviceProvider) =>
        serviceProvider.GetService(settings.SerializerType) as IMessageSerializerProvider
            ?? throw new ConfigurationMessageBusException($"The bus {settings.Name} could not resolve the required {nameof(IMessageSerializerProvider)} of type {settings.SerializerType.Name} from {nameof(serviceProvider)}");
}
