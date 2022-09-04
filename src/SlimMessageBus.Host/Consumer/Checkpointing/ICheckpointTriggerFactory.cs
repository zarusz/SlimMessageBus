namespace SlimMessageBus.Host;

using SlimMessageBus.Host.Config;

public interface ICheckpointTriggerFactory
{
    ICheckpointTrigger Create(IEnumerable<AbstractConsumerSettings> consumerSettingsCollection);
}
