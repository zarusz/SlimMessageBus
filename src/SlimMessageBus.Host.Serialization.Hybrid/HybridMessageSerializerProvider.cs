namespace SlimMessageBus.Host.Serialization.Hybrid;

using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

/// <summary>
/// <see cref="IMessageSerializerProvider"/> implementation that delegates (routes) the serialization to the respective serializer based on message type.
/// </summary>
public class HybridMessageSerializerProvider : IMessageSerializerProvider
{
    private readonly ILogger _logger;
    private readonly Dictionary<Type, IMessageSerializerProvider> _serializerProviderByType = [];
    private readonly ConcurrentDictionary<string, IMessageSerializer> _serializerByPath = [];

    public IMessageSerializerProvider DefaultSerializer { get; set; }

    internal IReadOnlyDictionary<Type, IMessageSerializerProvider> SerializerByType => _serializerProviderByType;

    public HybridMessageSerializerProvider(ILogger<HybridMessageSerializerProvider> logger, IDictionary<IMessageSerializerProvider, Type[]> registration, IMessageSerializerProvider defaultMessageSerializer = null)
    {
        _logger = logger;
        DefaultSerializer = defaultMessageSerializer;
        foreach (var entry in registration)
        {
            Add(entry.Key, entry.Value);
        }
    }

    public void Add(IMessageSerializerProvider serializer, params Type[] supportedTypes)
    {
        if (serializer is null) throw new ArgumentNullException(nameof(serializer));

        DefaultSerializer ??= serializer;

        foreach (var type in supportedTypes)
        {
            _serializerProviderByType.Add(type, serializer);
        }
    }

    protected virtual IMessageSerializer MatchSerializer(string path, Type t)
    {
        if (!_serializerProviderByType.TryGetValue(t, out var serializer))
        {
            _logger.LogTrace("Serializer for type {MessageType} not registered, will use default serializer", t);

            if (DefaultSerializer == null)
            {
                throw new InvalidOperationException("No serializers registered.");
            }

            serializer = DefaultSerializer;
        }

        _logger.LogDebug("Serializer for type {MessageType} will be {Serializer}", t, serializer);
        return serializer.GetSerializer(path);
    }

    #region Implementation of IMessageSerializerProvider

    public IMessageSerializer GetSerializer(string path) => _serializerByPath.GetOrAdd(path, p => new HybidMessageSerializer(this, p));

    #endregion

    sealed class HybidMessageSerializer(HybridMessageSerializerProvider provider, string path) : IMessageSerializer
    {
        public object Deserialize(Type messageType, IReadOnlyDictionary<string, object> headers, byte[] payload, object transportMessage)
        {
            var serializer = provider.MatchSerializer(path, messageType);
            return serializer.Deserialize(messageType, headers, payload, transportMessage);
        }

        public byte[] Serialize(Type messageType, IDictionary<string, object> headers, object message, object transportMessage)
        {
            var serializer = provider.MatchSerializer(path, messageType);
            return serializer.Serialize(messageType, headers, message, transportMessage);
        }
    }
}

