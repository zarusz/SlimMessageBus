using System.Threading;

namespace SlimMessageBus.Host
{
    public interface IConsumerContextAware
    {
        AsyncLocal<ConsumerContext> Context { get; }
    }
}
