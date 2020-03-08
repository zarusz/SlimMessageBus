using Common.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace SlimMessageBus.Host.Serialization.Avro
{
    /// <summary>
    /// Strategy to creates message instances using reflection.
    /// Constructor objects <see cref="ConstructorInfo"> are cached.
    /// </summary>
    public class ReflectionMessageCreationStategy : IMessageCreationStrategy
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly IDictionary<Type, ConstructorInfo> _constructorByType = new Dictionary<Type, ConstructorInfo>();
        private readonly object _constructorByTypeLock = new object();

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
                Log.DebugFormat(CultureInfo.InvariantCulture, "Instantiating type {0}", type);

                var constructor = GetTypeConstructorSafe(type);
                return constructor.Invoke(null);
            }
            catch (Exception e)
            {
                Log.ErrorFormat(CultureInfo.InvariantCulture, "Error intantiating message type {0}", e, type);
                throw;
            }
        }
    }
}
