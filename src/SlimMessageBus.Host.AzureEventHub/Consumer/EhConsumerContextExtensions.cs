﻿namespace SlimMessageBus.Host.AzureEventHub
{
    using Azure.Messaging.EventHubs;
    using System;

    public static class EhConsumerContextExtensions
    {
        private const string MessageKey = "Eh_Message";

        public static EventData GetTransportMessage(this IConsumerContext context)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));

            return context.GetPropertyOrDefault<EventData>(MessageKey);
        }

        public static void SetTransportMessage(this ConsumerContext context, EventData message)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));

            context.SetProperty(MessageKey, message);
        }
    }
}