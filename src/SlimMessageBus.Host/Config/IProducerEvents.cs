namespace SlimMessageBus.Host.Config
{
    using System;

    public interface IProducerEvents
    {
        /// <summary>
        /// Called whenever a message is produced.
        /// </summary>
        Action<IMessageBus, ProducerSettings, object, string> OnMessageProduced { get; set; }
    }
}