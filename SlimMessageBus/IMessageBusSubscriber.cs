namespace SlimMessageBus
{
    /// <summary>
    /// The subscriber interface of the MessageBus
    /// </summary>
    public interface IMessageBusSubscriber
    {
        void Subscribe<TMessage>(IHandles<TMessage> msg);
        void UnSubscribe<TMessage>(IHandles<TMessage> msg);
    }
}