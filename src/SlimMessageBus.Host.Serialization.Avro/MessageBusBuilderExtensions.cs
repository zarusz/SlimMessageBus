namespace SlimMessageBus.Host.Serialization.Avro;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using SlimMessageBus.Host.Config;

public static class MessageBusBuilderExtensions
{
    /// <summary>
    /// Registers the <see cref="IMessageSerializer"/> with implementation as <see cref="AvroMessageSerializer"/>.
    /// </summary>
    /// <param name="mbb"></param>
    /// <param name="messageCreationStrategy"></param>
    /// <param name="schemaLookupStrategy"></param>
    /// <returns></returns>
    public static MessageBusBuilder AddAvroSerializer(this MessageBusBuilder mbb, IMessageCreationStrategy messageCreationStrategy, ISchemaLookupStrategy schemaLookupStrategy)
    {
        if (mbb.Services is not null)
        {
            mbb.Services.AddSingleton(svp => new AvroMessageSerializer(svp.GetRequiredService<ILoggerFactory>(), messageCreationStrategy, schemaLookupStrategy));
            mbb.Services.TryAddSingleton<IMessageSerializer>(svp => svp.GetRequiredService<AvroMessageSerializer>());
        }
        return mbb;
    }

    /// <summary>
    /// Registers the <see cref="IMessageSerializer"/> with implementation as <see cref="AvroMessageSerializer"/>.
    /// Uses <see cref="ReflectionSchemaLookupStrategy"/> and <see cref="ReflectionMessageCreationStategy"/> strategies.
    /// </summary>
    /// <param name="mbb"></param>
    /// <returns></returns>
    public static MessageBusBuilder AddAvroSerializer(this MessageBusBuilder mbb)
    {
        if (mbb.Services is not null)
        {
            mbb.Services.AddSingleton(svp => new AvroMessageSerializer(svp.GetRequiredService<ILoggerFactory>()));
            mbb.Services.TryAddSingleton<IMessageSerializer>(svp => svp.GetRequiredService<AvroMessageSerializer>());
        }
        return mbb;
    }
}
