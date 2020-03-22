using System;

namespace SlimMessageBus.Host.Config
{
    public interface IProducerEvents
    {
        /// <summary>
        /// Called whenever a message is produced.
        /// </summary>
        Action<IMessageBus, ProducerSettings, object, string> OnMessageProduced { get; set; }
    }
}