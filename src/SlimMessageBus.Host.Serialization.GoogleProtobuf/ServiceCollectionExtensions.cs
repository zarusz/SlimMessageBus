namespace SlimMessageBus.Host.Serialization.GoogleProtobuf;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="IMessageSerializer"/> with implementation as <see cref="GoogleProtobufMessageSerializer"/>.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="messageParserFactory"></param>
    /// <returns></returns>
    public static IServiceCollection AddMessageBusGoogleProtobufSerializer(this IServiceCollection services, IMessageParserFactory messageParserFactory = null)
    {
        services.AddSingleton(svp => new GoogleProtobufMessageSerializer(svp.GetRequiredService<ILoggerFactory>(), messageParserFactory));
        services.TryAddSingleton<IMessageSerializer>(svp => svp.GetRequiredService<GoogleProtobufMessageSerializer>());
        return services;
    }
}
