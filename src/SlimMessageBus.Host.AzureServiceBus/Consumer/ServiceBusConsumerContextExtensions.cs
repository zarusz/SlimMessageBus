namespace SlimMessageBus.Host.AzureServiceBus
{
    using Microsoft.Azure.ServiceBus;
    using System;

    public static class ServiceBusConsumerContextExtensions
    {
        private const string MessageKey = "ServiceBus_Message";

        public static Message GetTransportMessage(this IConsumerContext context)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));

            return context.GetPropertyOrDefault<Message>(MessageKey);
        }

        public static void SetTransportMessage(this ConsumerContext context, Message message)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));

            context.SetProperty(MessageKey, message);
        }
    }
}
