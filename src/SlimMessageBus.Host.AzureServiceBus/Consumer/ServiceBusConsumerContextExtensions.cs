using Microsoft.Azure.ServiceBus;
using System;

namespace SlimMessageBus.Host.AzureServiceBus
{
    public static class ServiceBusConsumerContextExtensions
    {
        private const string MessageKey = "ServiceBus_Message";

        public static Message GetTransportMessage(this ConsumerContext context)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));

            return context.GetOrDefault<Message>(MessageKey, null);
        }

        public static void SetTransportMessage(this ConsumerContext context, Message message)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));

            context.Properties[MessageKey] = message;
        }
    }
}
