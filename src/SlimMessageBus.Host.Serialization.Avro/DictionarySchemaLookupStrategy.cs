namespace SlimMessageBus.Host.Serialization.Avro;

using global::Avro;

/// <summary>
/// Stategy to lookup meessage schema by type using a dictionary registry
/// </summary>
public class DictionarySchemaLookupStrategy : ISchemaLookupStrategy
{
    private readonly ILogger _logger;
    private readonly IDictionary<Type, Schema> _registry;

    public DictionarySchemaLookupStrategy(ILogger<DictionarySchemaLookupStrategy> logger)
        : this(logger, new Dictionary<Type, Schema>())
    {
    }

    public DictionarySchemaLookupStrategy(ILogger<DictionarySchemaLookupStrategy> logger, IDictionary<Type, Schema> registry)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public void Add(Type type, Schema schema)
    {
        _registry.Add(type, schema);
    }

    public virtual Schema Lookup(Type type)
    {
        if (!_registry.TryGetValue(type, out var schema))
        {
            var msg = $"The type {type} does not have a schema registered. Check your configuration.";
            _logger.LogError(msg);
            throw new InvalidOperationException(msg);
        }

        _logger.LogDebug("Schema for type {0} is {1}", type, schema);
        return schema;
    }
}
