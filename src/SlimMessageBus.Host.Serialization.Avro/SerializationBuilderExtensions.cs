namespace SlimMessageBus.Host.Serialization.Avro;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using SlimMessageBus.Host;

public static class SerializationBuilderExtensions
{
    /// <summary>
    /// Registers the <see cref="IMessageSerializer"/> with implementation as <see cref="AvroMessageSerializer"/>.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="messageCreationStrategy"></param>
    /// <param name="schemaLookupStrategy"></param>
    /// <returns></returns>
    public static TBuilder AddAvroSerializer<TBuilder>(this TBuilder builder, IMessageCreationStrategy messageCreationStrategy, ISchemaLookupStrategy schemaLookupStrategy)
        where TBuilder : ISerializationBuilder
    {
        builder.RegisterSerializer<AvroMessageSerializer>(services =>
        {
            services.TryAddSingleton(svp => new AvroMessageSerializer(svp.GetRequiredService<ILoggerFactory>(), messageCreationStrategy, schemaLookupStrategy));
        });
        return builder;
    }

    /// <summary>
    /// Registers the <see cref="IMessageSerializer"/> with implementation as <see cref="AvroMessageSerializer"/>.
    /// Uses <see cref="ReflectionSchemaLookupStrategy"/> and <see cref="ReflectionMessageCreationStrategy"/> strategies.
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static TBuilder AddAvroSerializer<TBuilder>(this TBuilder builder)
        where TBuilder : ISerializationBuilder
    {
        builder.RegisterSerializer<AvroMessageSerializer>(services =>
        {
            services.TryAddSingleton(svp => new AvroMessageSerializer(svp.GetRequiredService<ILoggerFactory>()));
        });
        return builder;
    }
}
