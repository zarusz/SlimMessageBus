namespace SlimMessageBus.Host.Collections;

/// <summary>
/// Collection of producer settings indexed by message type (including base types).
/// The message type hierarchy is discovered at runtime and cached for faster access.
/// </summary>
/// <typeparam name="TProducer">The producer type</typeparam>
public partial class ProducerByMessageTypeCache<TProducer> : IReadOnlyCache<Type, TProducer>
    where TProducer : class
{
    private readonly ILogger _logger;
    private readonly IRuntimeTypeCache _runtimeTypeCache;
    private readonly IDictionary<Type, TProducer> _producerByBaseType;
    private readonly IReadOnlyCache<Type, TProducer> _producerByType;

    public ProducerByMessageTypeCache(ILogger logger, IDictionary<Type, TProducer> producerByBaseType, IRuntimeTypeCache runtimeTypeCache)
    {
        _logger = logger;
        _runtimeTypeCache = runtimeTypeCache;
        _producerByBaseType = producerByBaseType;
        _producerByType = new SafeDictionaryWrapper<Type, TProducer>(CalculateProducer);
    }

    /// <summary>
    /// Find the nearest base type (or exact type) that has the producer declared (from the dictionary).
    /// </summary>
    /// <param name="messageType"></param>
    /// <returns>Producer when found, else null</returns>
    public TProducer this[Type messageType] => _producerByType[messageType];

    private TProducer CalculateProducer(Type messageType)
    {
        var assignableProducers = _producerByBaseType
            .Where(x => _runtimeTypeCache.IsAssignableFrom(messageType, x.Key))
            .OrderBy(x => CalculateBaseClassDistance(messageType, x.Key));

        var assignableProducer = assignableProducers.FirstOrDefault();
        if (assignableProducer.Key != null)
        {
            LogMatchedProducerForMessageType(messageType, assignableProducer.Key);
            return assignableProducer.Value;
        }

        // Is is collection of message types?
        var collectionInfo = _runtimeTypeCache.GetCollectionTypeInfo(messageType);
        if (collectionInfo != null)
        {
            var innerProducer = CalculateProducer(collectionInfo.ItemType);
            if (innerProducer != null)
            {
                return innerProducer;
            }
        }

        LogUnmatchedProducerForMessageType(messageType);

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

    #region Logging

    [LoggerMessage(
       EventId = 0,
       Level = LogLevel.Debug,
       Message = "Matched producer for message type {ProducerMessageType} for dispatched message type {MessageType}")]
    private partial void LogMatchedProducerForMessageType(Type messageType, Type producerMessageType);

    [LoggerMessage(
       EventId = 1,
       Level = LogLevel.Debug,
       Message = "Unable to match any declared producer for dispatched message type {MessageType}")]
    private partial void LogUnmatchedProducerForMessageType(Type messageType);

    #endregion
}

#if NETSTANDARD2_0

public partial class ProducerByMessageTypeCache<TProducer>
{
    private partial void LogMatchedProducerForMessageType(Type messageType, Type producerMessageType)
        => _logger.LogDebug("Matched producer for message type {ProducerMessageType} for dispatched message type {MessageType}", producerMessageType, messageType);

    private partial void LogUnmatchedProducerForMessageType(Type messageType)
        => _logger.LogDebug("Unable to match any declared producer for dispatched message type {MessageType}", messageType);
}

#endif