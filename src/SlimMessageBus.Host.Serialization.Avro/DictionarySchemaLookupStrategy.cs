using Avro;
using Common.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace SlimMessageBus.Host.Serialization.Avro
{
    /// <summary>
    /// Stategy to lookup meessage schema by type using a dictionary registry
    /// </summary>
    public class DictionarySchemaLookupStrategy : ISchemaLookupStrategy
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly IDictionary<Type, Schema> _registry;

        public DictionarySchemaLookupStrategy()
            : this(new Dictionary<Type, Schema>())
        {
        }

        public DictionarySchemaLookupStrategy(IDictionary<Type, Schema> registry)
        {
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
                Log.Error(msg);
                throw new InvalidOperationException(msg);
            }

            Log.DebugFormat(CultureInfo.InvariantCulture, "Schema for type {0} is {1}", type, schema);
            return schema;
        }
    }
}
