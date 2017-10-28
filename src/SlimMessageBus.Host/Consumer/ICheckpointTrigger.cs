namespace SlimMessageBus.Host
{
    public interface ICheckpointTrigger
    {
        bool IsEnabled { get; }
        bool Increment();
        void Reset();
    }
}