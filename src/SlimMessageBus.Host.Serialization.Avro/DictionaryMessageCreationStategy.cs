namespace SlimMessageBus.Host.Serialization.Avro
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Strategy to create message instances using a dictionary which holds registered factory methods.
    /// This should be faster than the <see cref="ReflectionMessageCreationStategy"/> strategy.
    /// </summary>
    public class DictionaryMessageCreationStategy : IMessageCreationStrategy
    {
        private readonly ILogger _logger;
        private readonly IDictionary<Type, Func<object>> _registry;

        public DictionaryMessageCreationStategy(ILogger<DictionaryMessageCreationStategy> logger)
            : this(logger, new Dictionary<Type, Func<object>>())
        {
        }

        public DictionaryMessageCreationStategy(ILogger<DictionaryMessageCreationStategy> logger, IDictionary<Type, Func<object>> registry)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public void Add(Type type, Func<object> factory)
        {
            _registry.Add(type, factory);
        }

        public virtual object Create(Type type)
        {
            if (!_registry.TryGetValue(type, out var factory))
            {
                var msg = $"The type {type} does not have a factory registered. Check your configuration.";
                _logger.LogError(msg);
                throw new InvalidOperationException(msg);
            }
            return factory();
        }
    }
}
