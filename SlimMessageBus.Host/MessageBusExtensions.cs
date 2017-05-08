namespace SlimMessageBus.Host
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