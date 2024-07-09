namespace SlimMessageBus.Host.Serialization.Avro;

using System.Reflection;

/// <summary>
/// Strategy to creates message instances using reflection.
/// Constructor objects <see cref="ConstructorInfo"> are cached.
/// </summary>
public class ReflectionMessageCreationStrategy : IMessageCreationStrategy
{
    private readonly ILogger _logger;
    private readonly IDictionary<Type, ConstructorInfo> _constructorByType = new Dictionary<Type, ConstructorInfo>();
    private readonly object _constructorByTypeLock = new();

    public ReflectionMessageCreationStrategy(ILogger logger)
    {
        _logger = logger;
    }

    protected virtual ConstructorInfo GetTypeConstructorSafe(Type type)
    {
        if (!_constructorByType.TryGetValue(type, out var constructor))
        {
            constructor = type.GetConstructor(Type.EmptyTypes);
            if (constructor == null)
            {
                throw new InvalidOperationException($"The type {type} does not have a parameterless constructor");
            }

            lock (_constructorByTypeLock)
            {
                // Note of the key entry is already added it will be overridden here (expected)
                _constructorByType[type] = constructor;
            }
        }

        return constructor;
    }

    public virtual object Create(Type type)
    {
        try
        {
            // by default create types using reflection
            _logger.LogDebug("Instantiating type {Type}", type);

            var constructor = GetTypeConstructorSafe(type);
            return constructor.Invoke(null);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error instantiating message type {Type}", type);
            throw;
        }
    }
}
