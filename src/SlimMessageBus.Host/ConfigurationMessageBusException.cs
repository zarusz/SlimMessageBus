namespace SlimMessageBus.Host
{
    using System;

    public class ConfigurationMessageBusException : MessageBusException
    {
        public ConfigurationMessageBusException()
        {
        }

        public ConfigurationMessageBusException(string message) : base(message)
        {
        }

        public ConfigurationMessageBusException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
