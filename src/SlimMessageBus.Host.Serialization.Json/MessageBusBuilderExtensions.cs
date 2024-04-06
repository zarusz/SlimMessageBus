namespace SlimMessageBus.Host.Serialization.Json;

using System.Text;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using SlimMessageBus.Host;
using SlimMessageBus.Host.Builders;

public static class MessageBusBuilderExtensions
{
    /// <summary>
    /// Registers the <see cref="IMessageSerializer"/> with implementation as <see cref="JsonMessageSerializer"/>.
    /// </summary>
    /// <param name="mbb"></param>
    /// <param name="encoding">When not provided defaults to <see cref="Encoding.UTF8"></param>
    /// <param name="jsonSerializerSettings"></param>
    /// <returns></returns>
    public static MessageBusBuilder AddJsonSerializer(this MessageBusBuilder mbb, Encoding encoding = null, JsonSerializerSettings jsonSerializerSettings = null)
    {
        mbb.PostConfigurationActions.Add(services =>
        {
            services.TryAddSingleton(svp => new JsonMessageSerializer(jsonSerializerSettings ?? svp.GetService<JsonSerializerSettings>(), encoding, svp.GetRequiredService<ILogger<JsonMessageSerializer>>()));
            services.TryAddSingleton<IMessageSerializer>(svp => svp.GetRequiredService<JsonMessageSerializer>());
        });
        return mbb;
    }

    /// <summary>
    /// Registers the <see cref="IMessageSerializer"/> with implementation as <see cref="JsonMessageSerializer"/>.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="encoding">When not provided defaults to <see cref="Encoding.UTF8"></param>
    /// <param name="jsonSerializerSettings"></param>
    /// <returns></returns>
    public static IHybridSerializerBuilderOptions AddJsonSerializer(this IHybridSerializationBuilder builder, Encoding encoding = null, JsonSerializerSettings jsonSerializerSettings = null)
    {
        return builder.RegisterSerializer<JsonMessageSerializer>(services =>
        {
            services.TryAddSingleton(svp => new JsonMessageSerializer(jsonSerializerSettings ?? svp.GetService<JsonSerializerSettings>(), encoding, svp.GetRequiredService<ILogger<JsonMessageSerializer>>()));
        });
    }
}
