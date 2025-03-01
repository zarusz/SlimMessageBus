namespace SlimMessageBus.Host;

public static class MessageBusSettingsExtensions
{
    public static IMessageSerializer GetSerializer(this MessageBusSettings settings, IServiceProvider serviceProvider) =>
        (IMessageSerializer)serviceProvider.GetService(settings.SerializerType)
            ?? throw new ConfigurationMessageBusException($"The bus {settings.Name} could not resolve the required message serializer type {settings.SerializerType.Name} from {nameof(serviceProvider)}");

    public static IMessageSerializerProvider GetSerializerProvider(this MessageBusSettings settings, IServiceProvider serviceProvider) =>
        (IMessageSerializerProvider)serviceProvider.GetService(settings.SerializerType)
            ?? throw new ConfigurationMessageBusException($"The bus {settings.Name} could not resolve the required message serializer type {settings.SerializerType.Name} from {nameof(serviceProvider)}");
}
