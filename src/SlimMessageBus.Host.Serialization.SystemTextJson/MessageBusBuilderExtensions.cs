namespace SlimMessageBus.Host.Serialization.SystemTextJson;

using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using SlimMessageBus.Host.Config;

public static class MessageBusBuilderExtensions
{
    /// <summary>
    /// Registers the <see cref="IMessageSerializer"/> with implementation as <see cref="JsonMessageSerializer"/>.
    /// </summary>
    /// <param name="mbb"></param>
    /// <returns></returns>
    public static MessageBusBuilder AddJsonSerializer(this MessageBusBuilder mbb, JsonSerializerOptions options = null)
    {
        if (mbb.Services is not null)
        {
            mbb.Services.AddSingleton(svp => new JsonMessageSerializer(options ?? svp.GetService<JsonSerializerOptions>()));
            mbb.Services.TryAddSingleton<IMessageSerializer>(svp => svp.GetRequiredService<JsonMessageSerializer>());
        }
        return mbb;
    }
}
