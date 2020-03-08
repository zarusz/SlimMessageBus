using Common.Logging;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace SlimMessageBus.Host.Serialization.Avro
{
    /// <summary>
    /// Strategy to create message instances using a dictionary which holds registered factory methods.
    /// This should be faster than the <see cref="ReflectionMessageCreationStategy"/> strategy.
    /// </summary>
    public class DictionaryMessageCreationStategy : IMessageCreationStrategy
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly IDictionary<Type, Func<object>> _registry;

        public DictionaryMessageCreationStategy()
            : this(new Dictionary<Type, Func<object>>())
        {
        }

        public DictionaryMessageCreationStategy(IDictionary<Type, Func<object>> registry)
        {
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
                Log.Error(msg);
                throw new InvalidOperationException(msg);
            }
            return factory();
        }
    }   
}
