namespace SlimMessageBus
{

    /// <summary>
    /// The publisher interface of the MessageBus
    /// </summary>
    public interface IMessageBusPublisher
    {
        void Publish<TMessage>(TMessage msg);
    }
}