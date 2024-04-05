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
    /// <returns></returns>
    public static TBuilder AddJsonSerializer<TBuilder>(this TBuilder builder, JsonSerializerOptions options = null)
        where TBuilder : ISerializationBuilder
    {
        builder.PostConfigurationActions.Add(services =>
        {
            services.TryAddSingleton(svp => new JsonMessageSerializer(options ?? svp.GetService<JsonSerializerOptions>()));
            services.TryAddSingleton<IMessageSerializer>(svp => svp.GetRequiredService<JsonMessageSerializer>());
        });
        return builder;
    }
}
