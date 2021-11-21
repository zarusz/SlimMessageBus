namespace SlimMessageBus.Host.AzureServiceBus.Consumer
{
    using Microsoft.Azure.ServiceBus;
    using SlimMessageBus.Host.Config;

    public class ConsumerInvoker
    {
        public IMessageProcessor<Message> Processor { get; }
        public IMessageTypeConsumerInvokerSettings Invoker { get; }

        public ConsumerInvoker(IMessageProcessor<Message> processor, IMessageTypeConsumerInvokerSettings invoker)
        {
            Processor = processor;
            Invoker = invoker;
        }
    }
}