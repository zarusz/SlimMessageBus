namespace SlimMessageBus.Core.Config
{
    public static class MessageBusConfigExtensions
    {
        public static MessageBusBuilder Configure(this SimpleMessageBus bus)
        {
            return new MessageBusBuilder();
        }
    }
}