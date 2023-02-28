namespace SlimMessageBus.Host.Serialization.Json;

using System.Text;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="IMessageSerializer"/> with implementation as <see cref="JsonMessageSerializer"/>.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="encoding">When not provided defaults to <see cref="Encoding.UTF8"></param>
    /// <param name="jsonSerializerSettings"></param>
    /// <returns></returns>
    public static IServiceCollection AddMessageBusJsonSerializer(this IServiceCollection services, Encoding encoding = null, JsonSerializerSettings jsonSerializerSettings = null)
    {
        services.AddSingleton(svp => new JsonMessageSerializer(jsonSerializerSettings ?? svp.GetService<JsonSerializerSettings>(), encoding, svp.GetRequiredService<ILogger<JsonMessageSerializer>>()));
        services.TryAddSingleton<IMessageSerializer>(svp => svp.GetRequiredService<JsonMessageSerializer>());
        return services;
    }
}
