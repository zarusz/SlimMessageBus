namespace SlimMessageBus.Host.Serialization.Json;

using System.Text;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using SlimMessageBus.Host;

public static class SerializationBuilderExtensions
{
    /// <summary>
    /// Registers the <see cref="IMessageSerializer"/> with implementation as <see cref="JsonMessageSerializer"/>.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="encoding">When not provided defaults to <see cref="Encoding.UTF8"></param>
    /// <param name="jsonSerializerSettings"></param>
    /// <returns></returns>
    public static TBuilder AddJsonSerializer<TBuilder>(this TBuilder builder, Encoding encoding = null, JsonSerializerSettings jsonSerializerSettings = null)
        where TBuilder : ISerializationBuilder
    {
        builder.RegisterSerializer<JsonMessageSerializer>(services =>
        {
            // Add the implementation
            services.TryAddSingleton(svp => new JsonMessageSerializer(jsonSerializerSettings ?? svp.GetService<JsonSerializerSettings>(), encoding, svp.GetRequiredService<ILogger<JsonMessageSerializer>>()));
            // Add the serializer as IMessageSerializer<string>
            services.TryAddSingleton(svp => svp.GetRequiredService<JsonMessageSerializer>() as IMessageSerializer<string>);
        });
        return builder;
    }
}
