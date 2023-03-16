namespace SlimMessageBus.Host.Serialization.Hybrid;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using SlimMessageBus.Host.Config;

public static class MessageBusBuilderExtensions
{
    /// <summary>
    /// Registers the <see cref="IMessageSerializer"/> with implementation as <see cref="HybridMessageSerializer"/>.
    /// </summary>
    /// <param name="mbb"></param>
    /// <param name="registration"></param>
    /// <param name="defaultMessageSerializer">The default serializer to be used when the message type cannot be matched</param>
    /// <returns></returns>
    public static MessageBusBuilder AddHybridSerializer(this MessageBusBuilder mbb, IDictionary<IMessageSerializer, Type[]> registration, IMessageSerializer defaultMessageSerializer)
    {
        mbb.PostConfigurationActions.Add(services =>
        {
            services.TryAddSingleton(svp => new HybridMessageSerializer(svp.GetRequiredService<ILogger<HybridMessageSerializer>>(), registration, defaultMessageSerializer));
            services.TryAddSingleton<IMessageSerializer>(svp => svp.GetRequiredService<HybridMessageSerializer>());
        });
        return mbb;
    }
}
