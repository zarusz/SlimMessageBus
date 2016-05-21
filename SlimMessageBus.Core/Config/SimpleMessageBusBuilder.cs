namespace SlimMessageBus.Core.Config
{
    public class SimpleMessageBusBuilder : MessageBusBuilder
    {
        public SimpleMessageBusBuilder(MessageBusBuilder prototype)
        {
            ResolverChain = prototype.ResolverChain;
        }
    }
}