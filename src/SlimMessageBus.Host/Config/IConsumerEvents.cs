using System;

namespace SlimMessageBus.Host.Config
{
    public interface IConsumerEvents
    {
        /// <summary>
        /// Called whenever a consumer receives an expired message.
        /// </summary>
        Action<ConsumerSettings, object> OnMessageExpired { get; set; }
        /// <summary>
        /// Called whenever a consumer errors out while processing the message.
        /// </summary>
        Action<ConsumerSettings, object, Exception> OnMessageFault { get; set; }
    }
}