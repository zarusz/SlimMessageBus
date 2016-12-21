namespace SlimMessageBus.Host.ServiceLocator
{
    public static class MessageBusExtensions
    {
        public static IMessageBus CurrentMessageBusComesFrom(this IMessageBus messageBus)
        {
            MessageBus.SetProvider(() => messageBus);
            return messageBus;
        }
    }
}