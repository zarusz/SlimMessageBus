namespace SlimMessageBus.Host.AzureServiceBus.Consumer
{
    using Azure.Messaging.ServiceBus;
    using SlimMessageBus.Host.Config;

    public class ConsumerInvoker
    {
        public IMessageProcessor<ServiceBusReceivedMessage> Processor { get; }
        public IMessageTypeConsumerInvokerSettings Invoker { get; }

        public ConsumerInvoker(IMessageProcessor<ServiceBusReceivedMessage> processor, IMessageTypeConsumerInvokerSettings invoker)
        {
            Processor = processor;
            Invoker = invoker;
        }
    }
}