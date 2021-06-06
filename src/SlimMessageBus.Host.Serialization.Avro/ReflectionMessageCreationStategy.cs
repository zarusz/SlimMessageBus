namespace SlimMessageBus.Host.Serialization.Avro
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    /// <summary>
    /// Strategy to creates message instances using reflection.
    /// Constructor objects <see cref="ConstructorInfo"> are cached.
    /// </summary>
    public class ReflectionMessageCreationStategy : IMessageCreationStrategy
    {
        private readonly ILogger _logger;
        private readonly IDictionary<Type, ConstructorInfo> _constructorByType = new Dictionary<Type, ConstructorInfo>();
        private readonly object _constructorByTypeLock = new object();

        public ReflectionMessageCreationStategy(ILogger logger)
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
                    throw new InvalidOperationException($"The type {type} does not have a paremeteless constructor");
                }

                lock (_constructorByTypeLock)
                {
                    // Note of the key entry is already added it will be overriden here (expected)
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
                _logger.LogDebug("Instantiating type {0}", type);

                var constructor = GetTypeConstructorSafe(type);
                return constructor.Invoke(null);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error intantiating message type {0}", type);
                throw;
            }
        }
    }
}
