﻿namespace SlimMessageBus.Host.Serialization.GoogleProtobuf;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using SlimMessageBus.Host.Config;

public static class MessageBusBuilderExtensions
{
    /// <summary>
    /// Registers the <see cref="IMessageSerializer"/> with implementation as <see cref="GoogleProtobufMessageSerializer"/>.
    /// </summary>
    /// <param name="mbb"></param>
    /// <param name="messageParserFactory"></param>
    /// <returns></returns>
    public static MessageBusBuilder AddGoogleProtobufSerializer(this MessageBusBuilder mbb, IMessageParserFactory messageParserFactory = null)
    {
        if (mbb.Services is not null)
        {
            mbb.Services.AddSingleton(svp => new GoogleProtobufMessageSerializer(svp.GetRequiredService<ILoggerFactory>(), messageParserFactory));
            mbb.Services.TryAddSingleton<IMessageSerializer>(svp => svp.GetRequiredService<GoogleProtobufMessageSerializer>());
        }
        return mbb;
    }
}
