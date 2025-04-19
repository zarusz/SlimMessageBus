namespace SlimMessageBus;

public interface IConsumerContextAccessor
{
    IConsumerContext? ConsumerContext { get; set; }
}