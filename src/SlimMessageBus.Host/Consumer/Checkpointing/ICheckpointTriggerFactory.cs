namespace SlimMessageBus.Host;

public interface ICheckpointTriggerFactory
{
    ICheckpointTrigger Create(IEnumerable<AbstractConsumerSettings> consumerSettingsCollection);
}
