namespace SlimMessageBus
{
    public interface IHandles<in TMessage>
    {
        void Handle(TMessage message);
    }
}
