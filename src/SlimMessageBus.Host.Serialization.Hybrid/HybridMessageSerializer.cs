namespace SlimMessageBus.Host.Serialization.Hybrid;

using Microsoft.Extensions.Logging;

/// <summary>
/// <see cref="IMessageSerializer"/> implementation that delegates (routes) the serialization to the respective serializer based on message type.
/// </summary>
public class HybridMessageSerializer : IMessageSerializer
{
    private readonly ILogger _logger;
    private readonly Dictionary<Type, IMessageSerializer> _serializerByType = [];

    public IMessageSerializer DefaultSerializer { get; set; }

    internal IReadOnlyDictionary<Type, IMessageSerializer> SerializerByType => _serializerByType;

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
#if NETSTANDARD2_0
        if (serializer is null) throw new ArgumentNullException(nameof(serializer));
#else
        ArgumentNullException.ThrowIfNull(serializer);
#endif

        DefaultSerializer ??= serializer;

        foreach (var type in supportedTypes)
        {
            _serializerByType.Add(type, serializer);
        }
    }

    protected virtual IMessageSerializer MatchSerializer(Type t)
    {
        if (!_serializerByType.TryGetValue(t, out var serializer))
        {
            _logger.LogTrace("Serializer for type {MessageType} not registered, will use default serializer", t);

            if (DefaultSerializer == null)
            {
                throw new InvalidOperationException("No serializers registered.");
            }

            serializer = DefaultSerializer;
        }

        _logger.LogDebug("Serializer for type {MessageType} will be {Serializer}", t, serializer);
        return serializer;
    }

    public object Deserialize(Type t, byte[] payload, IMessageContext context)
    {
        var serializer = MatchSerializer(t);
        return serializer.Deserialize(t, payload, context);
    }

    public byte[] Serialize(Type t, object message, IMessageContext context)
    {
        var serializer = MatchSerializer(t);
        return serializer.Serialize(t, message, context);
    }
}
