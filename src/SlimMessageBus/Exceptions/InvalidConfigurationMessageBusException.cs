using System;

namespace SlimMessageBus
{
    public class InvalidConfigurationMessageBusException : MessageBusException
    {
        public InvalidConfigurationMessageBusException(string message) : base(message)
        {
        }

        public InvalidConfigurationMessageBusException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}