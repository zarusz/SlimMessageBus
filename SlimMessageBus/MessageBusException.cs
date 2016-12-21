using System;

namespace SlimMessageBus
{
    public class MessageBusException : Exception
    {
        public MessageBusException(string message) : base(message)
        {
        }

        public MessageBusException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
