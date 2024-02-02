namespace SlimMessageBus.Host;

public class CheckpointTriggerFactory : ICheckpointTriggerFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly Func<IReadOnlyCollection<CheckpointValue>, string> _exceptionMessageFactory;

    public CheckpointTriggerFactory(ILoggerFactory loggerFactory, Func<IReadOnlyCollection<CheckpointValue>, string> exceptionMessageFactory)
    {
        _loggerFactory = loggerFactory;
        _exceptionMessageFactory = exceptionMessageFactory;
    }

    public ICheckpointTrigger Create(IEnumerable<AbstractConsumerSettings> consumerSettingsCollection)
    {
        // The first consumer settings will be taken (the assumption is that all consumers declared on same path/group will have a similar checkpoint settings).
        var consumerSettingsWithConfiguredCheckpoints = consumerSettingsCollection
            .Where(x => CheckpointTrigger.IsConfigured(x))
            .ToList();

        if (consumerSettingsWithConfiguredCheckpoints.Count > 1)
        {
            // Check if checkpoint settings across all the configured consumers is all the same.
            var configuredCheckpoints = consumerSettingsWithConfiguredCheckpoints.Select(CheckpointTrigger.GetCheckpointValue).ToHashSet();
            if (configuredCheckpoints.Count > 1)
            {
                var msg = _exceptionMessageFactory(configuredCheckpoints);
                throw new ConfigurationMessageBusException(msg);
            }
        }

        var consumerSettings = consumerSettingsWithConfiguredCheckpoints.FirstOrDefault() // take the configured checkpoint
            ?? consumerSettingsCollection.First(); // the checkpoint will be taken

        return new CheckpointTrigger(consumerSettings, _loggerFactory);
    }
}