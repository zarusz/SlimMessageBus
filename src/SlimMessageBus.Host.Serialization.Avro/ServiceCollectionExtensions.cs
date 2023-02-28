namespace SlimMessageBus.Host.Serialization.Avro;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="IMessageSerializer"/> with implementation as <see cref="AvroMessageSerializer"/>.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="messageCreationStrategy"></param>
    /// <param name="schemaLookupStrategy"></param>
    /// <returns></returns>
    public static IServiceCollection AddMessageBusAvroSerializer(this IServiceCollection services, IMessageCreationStrategy messageCreationStrategy, ISchemaLookupStrategy schemaLookupStrategy)
    {
        services.AddSingleton(svp => new AvroMessageSerializer(svp.GetRequiredService<ILoggerFactory>(), messageCreationStrategy, schemaLookupStrategy));
        services.TryAddSingleton<IMessageSerializer>(svp => svp.GetRequiredService<AvroMessageSerializer>());
        return services;
    }

    /// <summary>
    /// Registers the <see cref="IMessageSerializer"/> with implementation as <see cref="AvroMessageSerializer"/>.
    /// Uses <see cref="ReflectionSchemaLookupStrategy"/> and <see cref="ReflectionMessageCreationStategy"/> strategies.
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddMessageBusAvroSerializer(this IServiceCollection services)
    {
        services.AddSingleton(svp => new AvroMessageSerializer(svp.GetRequiredService<ILoggerFactory>()));
        services.TryAddSingleton<IMessageSerializer>(svp => svp.GetRequiredService<AvroMessageSerializer>());
        return services;
    }
}
