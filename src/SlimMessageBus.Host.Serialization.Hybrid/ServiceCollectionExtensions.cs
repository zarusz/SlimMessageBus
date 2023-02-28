namespace SlimMessageBus.Host.Serialization.Hybrid;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="IMessageSerializer"/> with implementation as <see cref="HybridMessageSerializer"/>.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="registration"></param>
    /// <param name="defaultMessageSerializer">The default serializer to be used when the message type cannot be matched</param>
    /// <returns></returns>
    public static IServiceCollection AddMessageBusHybridSerializer(this IServiceCollection services, IDictionary<IMessageSerializer, Type[]> registration, IMessageSerializer defaultMessageSerializer)
    {
        services.AddSingleton(svp => new HybridMessageSerializer(svp.GetRequiredService<ILogger<HybridMessageSerializer>>(), registration, defaultMessageSerializer));
        services.TryAddSingleton<IMessageSerializer>(svp => svp.GetRequiredService<HybridMessageSerializer>());
        return services;
    }
}
