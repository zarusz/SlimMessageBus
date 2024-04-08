namespace SlimMessageBus.Host.Serialization.GoogleProtobuf;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using SlimMessageBus.Host;

public static class SerializationBuilderExtensions
{
    /// <summary>
    /// Registers the <see cref="IMessageSerializer"/> with implementation as <see cref="GoogleProtobufMessageSerializer"/>.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="messageParserFactory"></param>
    /// <returns></returns>
    public static TBuilder AddGoogleProtobufSerializer<TBuilder>(this TBuilder builder, IMessageParserFactory messageParserFactory = null)
        where TBuilder : ISerializationBuilder
    {
        builder.RegisterSerializer<GoogleProtobufMessageSerializer>(services =>
        {
            services.TryAddSingleton(svp => new GoogleProtobufMessageSerializer(svp.GetRequiredService<ILoggerFactory>(), messageParserFactory));
        });
        return builder;
    }
}
