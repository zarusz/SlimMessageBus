namespace SlimMessageBus.Host.Serialization.Hybrid;

using Microsoft.Extensions.Logging;

/// <summary>
/// <see cref="IMessageSerializer"/> implementation that delegates (routes) the serialization to the respective serializer based on message type.
/// </summary>
public class HybridMessageSerializer : IMessageSerializer
{
    private readonly ILogger _logger;
    private readonly IList<IMessageSerializer> _serializers = new List<IMessageSerializer>();
    private readonly IDictionary<Type, IMessageSerializer> _serializerByType = new Dictionary<Type, IMessageSerializer>();
    public IMessageSerializer DefaultSerializer { get; set; }

    public HybridMessageSerializer(ILogger<HybridMessageSerializer> logger, IDictionary<IMessageSerializer, Type[]> registration, IMessageSerializer defaultMessageSerializer = null)
    {
        _logger = logger;
        DefaultSerializer = defaultMessageSerializer;
        foreach (var entry in registration)
        {
            Add(entry.Key, entry.Value);
        }
    }

    public void Add(IMessageSerializer serializer, params Type[] supportedTypes)
    {
        if (_serializers.Count == 0 && DefaultSerializer == null)
        {
            DefaultSerializer = serializer;
        }

        _serializers.Add(serializer);
        foreach (var type in supportedTypes)
        {
            _serializerByType.Add(type, serializer);
        }
    }

    protected virtual IMessageSerializer MatchSerializer(Type t)
    {
        if (_serializers.Count == 0)
        {
            throw new InvalidOperationException("No serializers registered.");
        }

        if (!_serializerByType.TryGetValue(t, out var serializer))
        {
            // use first as default
            _logger.LogTrace("Serializer for type {0} not registered, will use default serializer", t);
            serializer = DefaultSerializer;
        }

        _logger.LogDebug("Serializer for type {0} will be {1}", t, serializer);
        return serializer;
    }

    public object Deserialize(Type t, byte[] payload)
    {
        var serializer = MatchSerializer(t);
        return serializer.Deserialize(t, payload);
    }

    public byte[] Serialize(Type t, object message)
    {
        var serializer = MatchSerializer(t);
        return serializer.Serialize(t, message);
    }
}
