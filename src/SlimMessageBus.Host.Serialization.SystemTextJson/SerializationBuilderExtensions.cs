namespace SlimMessageBus.Host.Serialization.SystemTextJson;

using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using SlimMessageBus.Host;

public static class SerializationBuilderExtensions
{
    /// <summary>
    /// Registers the <see cref="IMessageSerializer"/> with implementation as <see cref="JsonMessageSerializer"/>.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="options"></param>
    /// <param name="useActualTypeOnSerialize">Should the actual message type be used during serialization? If false, the producer declared type will be passed (e.g. base type of the actual message). It will make a difference for polymorphic types.</param>
    /// <returns></returns>
    public static TBuilder AddJsonSerializer<TBuilder>(this TBuilder builder, JsonSerializerOptions options = null, bool useActualTypeOnSerialize = true)
        where TBuilder : ISerializationBuilder
    {
        builder.RegisterSerializer<JsonMessageSerializer>(services =>
        {
            // Add the implementation
            services.TryAddSingleton(svp => new JsonMessageSerializer(options ?? svp.GetService<JsonSerializerOptions>(), useActualTypeOnSerialize));
            // Add the serializer as IMessageSerializer<string>
            services.TryAddSingleton(svp => svp.GetRequiredService<JsonMessageSerializer>() as IMessageSerializer<string>);
        });
        return builder;
    }
}
