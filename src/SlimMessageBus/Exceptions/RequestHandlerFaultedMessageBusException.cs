using System;

namespace SlimMessageBus
{
    public class RequestHandlerFaultedMessageBusException : MessageBusException
    {
        public RequestHandlerFaultedMessageBusException(string message) : base(message)
        {
        }

        public RequestHandlerFaultedMessageBusException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}