namespace SlimMessageBus.Host.Collections;

public class KindMapping
{
    private readonly Dictionary<string, PathKind> _kindByTopic = [];
    private readonly Dictionary<Type, PathKind> _kindByMessageType = [];

    public void Configure(MessageBusSettings settings)
    {
        if (settings is null) throw new ArgumentNullException(nameof(settings));

        foreach (var producerSettings in settings.Producers)
        {
            var producerKind = producerSettings.PathKind;

            var path = producerSettings.DefaultPath;
            if (path != null)
            {
                AddPathMapping(producerKind, path);
            }

            AddTypeMapping(producerSettings, producerKind);
        }

        if (settings.RequestResponse != null)
        {
            AddPathMapping(settings.RequestResponse.PathKind, settings.RequestResponse.Path);
        }
    }

    private void AddTypeMapping(ProducerSettings producerSettings, PathKind producerKind)
    {
        if (_kindByMessageType.TryGetValue(producerSettings.MessageType, out var existingKind))
        {
            if (existingKind != producerKind)
            {
                throw new ConfigurationMessageBusException($"The same message type '{producerSettings.MessageType}' was used for queue and topic. You cannot share one message type for a topic and queue. Please fix your configuration.");
            }
        }
        else
        {
            _kindByMessageType.Add(producerSettings.MessageType, producerKind);
        }
    }

    private void AddPathMapping(PathKind producerKind, string path)
    {
        if (_kindByTopic.TryGetValue(path, out var existingKind))
        {
            if (existingKind != producerKind)
            {
                throw new ConfigurationMessageBusException($"The same name '{path}' was used for queue and topic. You cannot share one name for a topic and queue. Please fix your configuration.");
            }
        }
        else
        {
            _kindByTopic.Add(path, producerKind);
        }
    }

    public PathKind GetKind(Type messageType, string path)
    {
        // determine the SMB topic name if its a Azure SB queue or topic
        if (!_kindByTopic.TryGetValue(path, out var kind))
        {
            if (!_kindByMessageType.TryGetValue(messageType, out kind))
            {
                // by default this will be a topic
                kind = PathKind.Topic;
            }
        }

        return kind;
    }
}
