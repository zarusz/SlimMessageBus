namespace SlimMessageBus.Core.Config
{
    public static class MessageBusExtensions
    {
        public static IMessageBus MakeCurrentResolveIt(this IMessageBus messageBus)
        {
            MessageBus.SetProvider(() => messageBus);
            return messageBus;
        }
    }
}