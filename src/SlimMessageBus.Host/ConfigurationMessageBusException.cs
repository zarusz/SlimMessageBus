using System;

namespace SlimMessageBus.Host
{
    public class ConfigurationMessageBusException : MessageBusException
    {
        public ConfigurationMessageBusException(string message) : base(message)
        {
        }

        public ConfigurationMessageBusException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
