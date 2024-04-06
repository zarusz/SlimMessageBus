namespace SlimMessageBus.Host.Serialization.SystemTextJson;

using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using SlimMessageBus.Host;
using SlimMessageBus.Host.Builders;

public static class MessageBusBuilderExtensions
{
    /// <summary>
    /// Registers the <see cref="IMessageSerializer"/> with implementation as <see cref="JsonMessageSerializer"/>.
    /// </summary>
    /// <param name="mbb"></param>
    /// <returns></returns>
    public static MessageBusBuilder AddJsonSerializer(this MessageBusBuilder mbb, JsonSerializerOptions options = null)
    {
        mbb.PostConfigurationActions.Add(services =>
        {
            services.TryAddSingleton(svp => new JsonMessageSerializer(options ?? svp.GetService<JsonSerializerOptions>()));
            services.TryAddSingleton<IMessageSerializer>(svp => svp.GetRequiredService<JsonMessageSerializer>());
        });
        return mbb;
    }

    /// <summary>
    /// Registers the <see cref="IMessageSerializer"/> with implementation as <see cref="JsonMessageSerializer"/>.
    /// </summary>
    /// <param name="mbb"></param>
    /// <returns></returns>
    public static IHybridSerializerBuilderOptions AddJsonSerializer(this IHybridSerializationBuilder builder, JsonSerializerOptions options = null)
    {
        return builder.RegisterSerializer<JsonMessageSerializer>(services =>
        {
            services.TryAddSingleton(svp => new JsonMessageSerializer(options ?? svp.GetService<JsonSerializerOptions>()));
        });
    }
}
