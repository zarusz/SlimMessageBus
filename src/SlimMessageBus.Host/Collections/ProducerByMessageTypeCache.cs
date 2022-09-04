namespace SlimMessageBus.Host.Collections;

/// <summary>
/// Collection of producer settings indexed by message type (including base types).
/// The message type hierarchy is discovered at runtime and cached for faster access.
/// </summary>
/// <typeparam name="TProducer">The producer type</typeparam>
public class ProducerByMessageTypeCache<TProducer> where TProducer : class
{
    private readonly ILogger _logger;
    private readonly IDictionary<Type, TProducer> _producerByBaseType;
    private readonly IRuntimeTypeCache _runtimeTypeCache;
    private readonly SafeDictionaryWrapper<Type, TProducer> _producerByType;

    public ProducerByMessageTypeCache(ILogger logger, IDictionary<Type, TProducer> producerByBaseType, IRuntimeTypeCache runtimeTypeCache)
    {
        _logger = logger;
        _producerByBaseType = producerByBaseType;
        _runtimeTypeCache = runtimeTypeCache;
        _producerByType = new SafeDictionaryWrapper<Type, TProducer>();
    }

    public TProducer this[Type key] => GetProducer(key);

    /// <summary>
    /// Find the nearest base type (or exact type) that has the producer declared (from the dictionary).
    /// </summary>
    /// <param name="messageType"></param>
    /// <returns>Producer when found, else null</returns>
    public TProducer GetProducer(Type messageType)
        => _producerByType.GetOrAdd(messageType, CalculateProducer);

    private TProducer CalculateProducer(Type messageType)
    {
        var assignableProducers = _producerByBaseType.Where(x => _runtimeTypeCache.IsAssignableFrom(messageType, x.Key)).OrderBy(x => CalculateBaseClassDistance(messageType, x.Key));
        var assignableProducer = assignableProducers.FirstOrDefault();
        if (assignableProducer.Key != null)
        {
            _logger.LogDebug("Matched producer for message type {ProducerMessageType} for dispatched message type {MessageType}", assignableProducer.Key, messageType);
            return assignableProducer.Value;
        }

        _logger.LogDebug("Unable to match any declared producer for dispatched message type {MessageType}", messageType);

        // Note: Nulls are also added to dictionary, so that we don't look them up using reflection next time (cached).
        return null;
    }

    private static int CalculateBaseClassDistance(Type type, Type baseType)
    {
        if (!baseType.IsClass)
        {
            return 100;
        }

        var distance = 0;
        while (type != null && baseType != type)
        {
            distance++;
            type = type.BaseType;
        }

        return distance;
    }
}